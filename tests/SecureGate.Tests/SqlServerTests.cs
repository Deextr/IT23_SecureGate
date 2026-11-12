using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate.Tests;

public class SqlServerTests : IAsyncLifetime
{
    private readonly string _databaseName = "DB_SecureGate_Tests_" + Guid.NewGuid().ToString("N");
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private string ConnectionString =>
        $"Server=.\\SQLEXPRESS;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task RecoveryCode_IsStoredAsAHash_AndCanBeRedeemedOnlyOnce()
    {
        var userId = await CreateUserAsync("once");
        await using var db = CreateContext();
        var service = CreateRecoveryService(db);
        var codes = await service.GenerateAsync(userId, userId);
        var stored = await db.UserRecoveryCodes.AsNoTracking().Where(c => c.UserId == userId).ToListAsync();
        Assert.Equal(10, stored.Count);
        foreach (var row in stored)
        {
            var hashText = Convert.ToHexString(row.CodeHash);
            foreach (var code in codes)
            {
                Assert.DoesNotContain(code.Replace("-", "", StringComparison.Ordinal), hashText, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.True(await service.RedeemAsync(userId, codes[0].ToLowerInvariant()));
        Assert.False(await service.RedeemAsync(userId, codes[0]));
    }

    [Fact]
    public async Task RecoveryCode_ConcurrentRedemption_AllowsOnlyOneWinner()
    {
        var userId = await CreateUserAsync("race");
        string code;
        await using (var setup = CreateContext())
        {
            var codes = await CreateRecoveryService(setup).GenerateAsync(userId, userId);
            code = codes[0];
        }

        var results = await Task.WhenAll(
            RedeemOnNewContextAsync(userId, code),
            RedeemOnNewContextAsync(userId, code));

        Assert.Equal(1, results.Count(success => success));
        await using var db = CreateContext();
        var row = await db.UserRecoveryCodes.SingleAsync(c => c.UserId == userId && c.UsedAtUtc != null);
        Assert.NotNull(row.UsedAtUtc);
    }

    [Fact]
    public async Task AuditLog_RejectsUpdateAndDelete()
    {
        await using var db = CreateContext();
        db.AuthenticationAuditLogs.Add(new AuthenticationAuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            EventType = EventTypes.PasswordFailed,
            Outcome = Outcomes.Failure,
            Severity = Severities.Info,
            Details = "trigger test"
        });
        await db.SaveChangesAsync();

        var deleteError = await Record.ExceptionAsync(() => db.Database.ExecuteSqlRawAsync("DELETE FROM AuthenticationAuditLogs"));
        var updateError = await Record.ExceptionAsync(() => db.Database.ExecuteSqlRawAsync("UPDATE AuthenticationAuditLogs SET Outcome = 'Success'"));

        Assert.NotNull(deleteError);
        Assert.NotNull(updateError);
        Assert.Contains("append-only", deleteError!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("append-only", updateError!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.AuthenticationAuditLogs.CountAsync());
    }

    private async Task<bool> RedeemOnNewContextAsync(string userId, string code)
    {
        await using var db = CreateContext();
        return await CreateRecoveryService(db).RedeemAsync(userId, code);
    }

    private RecoveryCodeService CreateRecoveryService(ApplicationDbContext db)
    {
        var factory = new TestContextFactory(ConnectionString);
        var alerts = new AlertService(factory, TimeProvider.System);
        var audit = new AuditService(factory, alerts, new HttpContextAccessor(), TimeProvider.System, NullLogger<AuditService>.Instance);
        return new RecoveryCodeService(db, new RecoveryCodeKey(_key), Options.Create(new SecurityPolicyOptions()), audit, TimeProvider.System);
    }

    private async Task<string> CreateUserAsync(string name)
    {
        await using var db = CreateContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            Email = name + "@example.com",
            NormalizedEmail = (name + "@example.com").ToUpperInvariant(),
            DisplayName = "Test " + name,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options;
        return new ApplicationDbContext(options);
    }

    private sealed class TestContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly string _connectionString;

        public TestContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connectionString).Options;
            return new ApplicationDbContext(options);
        }
    }
}
