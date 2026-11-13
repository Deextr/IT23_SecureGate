using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate.Tests;

public class SecurityOperationsTests : IDisposable
{
    private readonly ServiceProvider _provider;

    public SecurityOperationsTests()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Options.Create(new SecurityPolicyOptions()));
        services.AddHttpContextAccessor();
        services.AddScoped<AlertService>();
        services.AddScoped<AuditService>();
        services.AddScoped<MonitoringService>();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        var now = DateTime.UtcNow;
        db.SecurityAlerts.AddRange(
            new SecurityAlert { RuleKey = "lockout", Title = "Account locked", Severity = Severities.High, Status = AlertStatuses.Open, CreatedAtUtc = now.AddHours(-2) },
            new SecurityAlert { RuleKey = "settings-changed", Title = "Settings changed", Severity = Severities.Critical, Status = AlertStatuses.Acknowledged, CreatedAtUtc = now.AddHours(-1) },
            new SecurityAlert { RuleKey = "ip-rate-limit", Title = "Rate limited", Severity = Severities.Medium, Status = AlertStatuses.Resolved, CreatedAtUtc = now });
        db.SaveChanges();

        var openAlert = db.SecurityAlerts.Single(a => a.Status == AlertStatuses.Open);
        db.InvestigationRecords.Add(new InvestigationRecord
        {
            AlertId = openAlert.AlertId,
            AssignedOfficerId = "officer-1",
            Status = InvestigationStatuses.Open,
            Notes = "Checking user activity",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        db.SaveChanges();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task SearchAlerts_Default_ExcludesResolvedUnlessFiltered()
    {
        await using var scope = _provider.CreateAsyncScope();
        var monitoring = scope.ServiceProvider.GetRequiredService<MonitoringService>();
        var page = await monitoring.SearchAlertsAsync(new AlertQuery { Page = 1, PageSize = 10 });
        Assert.Equal(2, page.Total);
        Assert.DoesNotContain(page.Items, item => item.Alert.Status == AlertStatuses.Resolved);
    }

    [Fact]
    public async Task SearchAlerts_InvestigationNoneFilter_OnlyAlertsWithoutInvestigation()
    {
        await using var scope = _provider.CreateAsyncScope();
        var monitoring = scope.ServiceProvider.GetRequiredService<MonitoringService>();
        var page = await monitoring.SearchAlertsAsync(new AlertQuery
        {
            InvestigationLink = "none",
            IncludeResolved = true,
            Page = 1,
            PageSize = 10
        });
        Assert.Equal(2, page.Total);
        Assert.All(page.Items, item => Assert.Null(item.InvestigationId));
        Assert.Contains(page.Items, item => item.Alert.Status == AlertStatuses.Acknowledged);
    }

    [Fact]
    public async Task ResolveAsync_ClosesLinkedInvestigation()
    {
        await using var scope = _provider.CreateAsyncScope();
        var alerts = scope.ServiceProvider.GetRequiredService<AlertService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alertId = await db.SecurityAlerts.Where(a => a.Status == AlertStatuses.Open).Select(a => a.AlertId).SingleAsync();

        var result = await alerts.ResolveAsync(alertId, "officer-1");
        Assert.True(result.Succeeded);

        var alert = await db.SecurityAlerts.AsNoTracking().SingleAsync(a => a.AlertId == alertId);
        var record = await db.InvestigationRecords.AsNoTracking().SingleAsync(i => i.AlertId == alertId);
        Assert.Equal(AlertStatuses.Resolved, alert.Status);
        Assert.Equal(InvestigationStatuses.Closed, record.Status);
        Assert.NotNull(record.ClosedAtUtc);
    }

    [Fact]
    public async Task SaveInvestigation_RejectsResolvedAlert()
    {
        await using var scope = _provider.CreateAsyncScope();
        var monitoring = scope.ServiceProvider.GetRequiredService<MonitoringService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alertId = await db.SecurityAlerts.Where(a => a.Status == AlertStatuses.Resolved).Select(a => a.AlertId).SingleAsync();

        var result = await monitoring.SaveInvestigationAsync(alertId, "officer-1", InvestigationStatuses.Open, "notes");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SaveInvestigation_ReusesExistingRecord_ForSameAlert()
    {
        await using var scope = _provider.CreateAsyncScope();
        var monitoring = scope.ServiceProvider.GetRequiredService<MonitoringService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alertId = await db.SecurityAlerts.Where(a => a.Status == AlertStatuses.Open).Select(a => a.AlertId).SingleAsync();

        var first = await monitoring.SaveInvestigationAsync(alertId, "officer-1", InvestigationStatuses.InProgress, "Updated notes");
        var second = await monitoring.SaveInvestigationAsync(alertId, "officer-2", InvestigationStatuses.Closed, "Closed notes");
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);

        var count = await db.InvestigationRecords.CountAsync(i => i.AlertId == alertId);
        Assert.Equal(1, count);
        var record = await db.InvestigationRecords.SingleAsync(i => i.AlertId == alertId);
        Assert.Equal(InvestigationStatuses.Closed, record.Status);
        Assert.Equal("Closed notes", record.Notes);
    }
}
