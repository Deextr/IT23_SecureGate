using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate.Tests;

public class PolicyAndAlertTests
{
    [Fact]
    public void SecuritySettings_RejectRetentionBelowOneYear()
    {
        var settings = new SecuritySettings { AuditRetentionDays = 30 };
        var errors = SecuritySettingsValidator.Validate(settings);
        Assert.Contains(errors, error => error.Contains("365", StringComparison.Ordinal));
    }

    [Fact]
    public void SecuritySettings_AcceptsTheApprovedBaseline()
    {
        var errors = SecuritySettingsValidator.Validate(new SecuritySettings());
        Assert.Empty(errors);
    }

    [Fact]
    public void SecurityPolicy_RejectsShortPasswordsAndLargeDrift()
    {
        var policy = new SecurityPolicyOptions { PasswordMinLength = 8, TotpDriftSteps = 3 };
        var errors = SecurityPolicyOptions.Validate(policy);
        Assert.Contains(errors, error => error.Contains("12", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("drift", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(EventTypes.AccountLocked, "lockout", Severities.High)]
    [InlineData(EventTypes.TotpCooldown, "totp-cooldown", Severities.High)]
    [InlineData(EventTypes.RecoveryCooldown, "recovery-cooldown", Severities.High)]
    [InlineData(EventTypes.RecoveryCodeSucceeded, "recovery-used", Severities.High)]
    [InlineData(EventTypes.MfaResetApproved, "mfa-reset", Severities.High)]
    [InlineData(EventTypes.RoleChanged, "role-change", Severities.High)]
    [InlineData(EventTypes.AccountDisabled, "account-disabled", Severities.High)]
    [InlineData(EventTypes.SecuritySettingsChanged, "settings-changed", Severities.High)]
    [InlineData(EventTypes.AccessDenied, "access-denied", Severities.High)]
    [InlineData(EventTypes.SessionRejected, "suspicious-session", Severities.High)]
    [InlineData(EventTypes.IpRateLimited, "ip-rate-limit", Severities.Medium)]
    public void AlertRules_ClassifyRequiredEvents(string eventType, string ruleKey, string severity)
    {
        var draft = AlertRules.FromEvent(new AuthenticationAuditLog { EventType = eventType, Details = "" });
        Assert.NotNull(draft);
        Assert.Equal(ruleKey, draft!.RuleKey);
        Assert.Equal(severity, draft.Severity);
    }

    [Fact]
    public void AlertRules_RaisesCriticalAlertOnlyForAdministratorCreation()
    {
        var admin = AlertRules.FromEvent(new AuthenticationAuditLog
        {
            EventType = EventTypes.AccountCreated,
            Details = "Role=Administrator"
        });
        var officer = AlertRules.FromEvent(new AuthenticationAuditLog
        {
            EventType = EventTypes.AccountCreated,
            Details = "Role=SecurityOfficer"
        });

        Assert.Equal(Severities.Critical, admin!.Severity);
        Assert.Null(officer);
    }

    [Fact]
    public void OrdinaryPasswordFailure_DoesNotCreateAnAlertByItself()
    {
        Assert.Null(AlertRules.FromEvent(new AuthenticationAuditLog { EventType = EventTypes.PasswordFailed }));
    }

    [Fact]
    public void SessionLifetime_ExpiresAfterEightHours_NotBefore()
    {
        var issued = DateTimeOffset.Parse("2026-11-11T00:00:00Z");
        Assert.False(SessionLifetime.IsAbsoluteExpired(issued, issued.AddHours(8), 8));
        Assert.True(SessionLifetime.IsAbsoluteExpired(issued, issued.AddHours(8).AddSeconds(1), 8));
    }

    [Fact]
    public void AttemptDelay_GrowsAndCaps()
    {
        Assert.Equal(200, AttemptDelay.Milliseconds(1));
        Assert.Equal(1000, AttemptDelay.Milliseconds(5));
        Assert.Equal(2000, AttemptDelay.Milliseconds(50));
    }

    [Fact]
    public void CsvFormatter_NeutralizesFormulas()
    {
        Assert.StartsWith("'", CsvFormatter.Cell("=cmd()"));
        Assert.Equal("\"a,b\"", CsvFormatter.Cell("a,b"));
    }
}
