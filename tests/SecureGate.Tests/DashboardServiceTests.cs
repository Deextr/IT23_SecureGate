using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;
using AppRoles = SecureGate1.Security.Roles;

namespace SecureGate.Tests;

public class DashboardServiceTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly DateTimeOffset _now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    public DashboardServiceTests()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(_now));
        services.AddSingleton(Options.Create(new SecurityPolicyOptions { MaxAuditRangeDays = 366 }));
        services.AddHttpContextAccessor();
        services.AddScoped<AlertService>();
        services.AddScoped<AuditService>();
        services.AddScoped<DashboardService>();
        _provider = services.BuildServiceProvider();
        SeedAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void DateRange_Last7Days_IncludesTodayAndSixPriorDays()
    {
        var range = DashboardDateRange.Resolve(
            DashboardPresets.Last7Days, null, null, new FixedTimeProvider(_now), TimeZoneInfo.Utc, 366, out var error);
        Assert.Null(error);
        Assert.Equal(new DateOnly(2026, 11, 7), range!.FromLocal);
        Assert.Equal(new DateOnly(2026, 11, 13), range.ToLocal);
        Assert.Equal(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.Equal(new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc), range.ToUtcExclusive);
    }

    [Fact]
    public void DateRange_Custom_RejectsInvertedDates()
    {
        var range = DashboardDateRange.Resolve(
            DashboardPresets.Custom,
            new DateOnly(2026, 11, 13),
            new DateOnly(2026, 11, 1),
            new FixedTimeProvider(_now),
            TimeZoneInfo.Utc,
            366,
            out var error);
        Assert.Null(range);
        Assert.Contains("end date", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DateRange_Custom_RejectsRangeBeyondPolicy()
    {
        var range = DashboardDateRange.Resolve(
            DashboardPresets.Custom,
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 11, 13),
            new FixedTimeProvider(_now),
            TimeZoneInfo.Utc,
            366,
            out var error);
        Assert.Null(range);
        Assert.Contains("366", error);
    }

    [Fact]
    public void KindFor_PrefersAdministratorOverOfficer()
    {
        using var scope = _provider.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<DashboardService>();
        var user = Principal("admin-1", AppRoles.Administrator, AppRoles.SecurityOfficer);
        Assert.Equal(DashboardKind.Administrator, dashboard.KindFor(user));
    }

    [Fact]
    public async Task AdminDashboard_UsesDatabaseCountsAndDateFilter()
    {
        using var scope = _provider.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<DashboardService>();
        var payload = await dashboard.GetAsync(Principal("admin-1", AppRoles.Administrator), DashboardPresets.Today, null, null);
        Assert.Equal(nameof(DashboardKind.Administrator), payload.Kind);
        Assert.Null(payload.Officer);
        Assert.Null(payload.Employee);
        Assert.NotNull(payload.Admin);
        Assert.Equal(4, payload.Admin!.TotalAccounts);
        Assert.Equal(3, payload.Admin.ActiveAccounts);
        Assert.Equal(1, payload.Admin.DisabledAccounts);
        Assert.Equal(1, payload.Admin.Administrators);
        Assert.Equal(1, payload.Admin.SecurityOfficers);
        Assert.Equal(2, payload.Admin.Employees);
        Assert.Equal(1, payload.Admin.PendingRecovery);
        Assert.Equal(1, payload.Admin.AlertsInRange);
        Assert.Equal(2, payload.Admin.AuditEventsInRange);
        Assert.Equal(1, payload.Admin.AccountsCreatedInRange);
    }

    [Fact]
    public async Task OfficerDashboard_OmitsAccountAdministrationTotals()
    {
        using var scope = _provider.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<DashboardService>();
        var payload = await dashboard.GetAsync(Principal("officer-1", AppRoles.SecurityOfficer), DashboardPresets.Today, null, null);
        Assert.Equal(nameof(DashboardKind.SecurityOfficer), payload.Kind);
        Assert.Null(payload.Admin);
        Assert.NotNull(payload.Officer);
        Assert.Equal(1, payload.Officer!.UnacknowledgedAlerts);
        Assert.Equal(1, payload.Officer.PendingInvestigations);
        Assert.Equal(1, payload.Officer.AlertsInRange);
    }

    [Fact]
    public async Task EmployeeDashboard_OnlyReturnsOwnRecords()
    {
        using var scope = _provider.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<DashboardService>();
        var payload = await dashboard.GetAsync(Principal("employee-1", AppRoles.Employee), DashboardPresets.Last7Days, null, null);
        Assert.Equal(nameof(DashboardKind.Employee), payload.Kind);
        Assert.Null(payload.Admin);
        Assert.Null(payload.Officer);
        Assert.NotNull(payload.Employee);
        Assert.Equal("Ada Employee", payload.Employee!.DisplayName);
        Assert.Equal(1, payload.Employee.PersonalEventsInRange);
        Assert.Single(payload.Employee.RecentActivity);
        Assert.Equal("Password failed", payload.Employee.RecentActivity[0].Title);
        Assert.Equal(1, payload.Employee.PendingRequests);
        Assert.DoesNotContain(payload.Employee.RecentActivity, item => item.Title.Contains("locked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EmptyRange_ReturnsZeroActivityNotNullCharts()
    {
        using var scope = _provider.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<DashboardService>();
        var payload = await dashboard.GetAsync(
            Principal("admin-1", AppRoles.Administrator),
            DashboardPresets.Custom,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 2));
        Assert.Null(payload.Error);
        Assert.Equal(0, payload.Admin!.AlertsInRange);
        Assert.Equal(0, payload.Admin.AuditEventsInRange);
        Assert.Empty(payload.Admin.RecentAudit);
        Assert.NotEmpty(payload.Admin.ActivityTrend.Labels);
        Assert.All(payload.Admin.ActivityTrend.Values, value => Assert.Equal(0, value));
    }

    private async Task SeedAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await CreateUserAsync(users, "admin-1", "admin@securegate.test", "Admin User", AppRoles.Administrator, true, _now.UtcDateTime.AddDays(-40));
        await CreateUserAsync(users, "officer-1", "so@securegate.test", "Sam Officer", AppRoles.SecurityOfficer, true, _now.UtcDateTime.AddDays(-20));
        await CreateUserAsync(users, "employee-1", "ada@securegate.test", "Ada Employee", AppRoles.Employee, true, _now.UtcDateTime.AddHours(-2));
        await CreateUserAsync(users, "employee-2", "other@securegate.test", "Other Employee", AppRoles.Employee, false, _now.UtcDateTime.AddDays(-10));

        db.AuthenticationAuditLogs.AddRange(
            new AuthenticationAuditLog
            {
                OccurredAtUtc = _now.UtcDateTime.AddHours(-1),
                EventType = EventTypes.PasswordFailed,
                Outcome = Outcomes.Failure,
                Severity = Severities.Medium,
                ActorUserId = "employee-1",
                TargetUserId = "employee-1"
            },
            new AuthenticationAuditLog
            {
                OccurredAtUtc = _now.UtcDateTime.AddHours(-2),
                EventType = EventTypes.AccountLocked,
                Outcome = Outcomes.Failure,
                Severity = Severities.High,
                ActorUserId = "employee-2",
                TargetUserId = "employee-2"
            },
            new AuthenticationAuditLog
            {
                OccurredAtUtc = _now.UtcDateTime.AddDays(-10),
                EventType = EventTypes.Logout,
                Outcome = Outcomes.Success,
                Severity = Severities.Info,
                ActorUserId = "employee-1",
                TargetUserId = "employee-1"
            });

        db.SecurityAlerts.Add(new SecurityAlert
        {
            RuleKey = "lockout",
            Title = "Account locked",
            Severity = Severities.High,
            Status = AlertStatuses.Open,
            TargetUserId = "employee-2",
            CreatedAtUtc = _now.UtcDateTime.AddHours(-2)
        });
        db.MfaResetRequests.Add(new MfaResetRequest
        {
            TargetUserId = "employee-1",
            RequesterUserId = "officer-1",
            VerificationMethod = VerificationMethods.InPerson,
            Notes = "Office visit",
            Status = ResetRequestStatuses.Pending,
            CreatedAtUtc = _now.UtcDateTime.AddHours(-3)
        });
        await db.SaveChangesAsync();

        var alertId = await db.SecurityAlerts.Select(a => a.AlertId).SingleAsync();
        db.InvestigationRecords.Add(new InvestigationRecord
        {
            AlertId = alertId,
            AssignedOfficerId = "officer-1",
            Status = InvestigationStatuses.Open,
            Notes = "Review lockout",
            CreatedAtUtc = _now.UtcDateTime.AddHours(-1),
            UpdatedAtUtc = _now.UtcDateTime.AddHours(-1)
        });
        await db.SaveChangesAsync();
    }

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> users,
        string id,
        string email,
        string name,
        string role,
        bool active,
        DateTime createdAtUtc)
    {
        var user = new ApplicationUser
        {
            Id = id,
            UserName = email,
            Email = email,
            DisplayName = name,
            EmailConfirmed = true,
            IsActive = active,
            CreatedAtUtc = createdAtUtc
        };
        var result = await users.CreateAsync(user, "SecureGate-Test-1!");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
    }

    private static ClaimsPrincipal Principal(string userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(AppClaims.Amr, AppClaims.Mfa)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
