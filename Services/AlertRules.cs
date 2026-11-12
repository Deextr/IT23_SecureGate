using SecureGate1.Models;

namespace SecureGate1.Services;

public sealed record AlertDraft(string RuleKey, string Title, string Severity, bool DedupeWhileOpen);

public static class AlertRules
{
    public static AlertDraft? FromEvent(AuthenticationAuditLog auditEvent)
    {
        return auditEvent.EventType switch
        {
            EventTypes.AccountLocked => new("lockout", "Account locked after repeated password failures", Severities.High, true),
            EventTypes.TotpCooldown => new("totp-cooldown", "Repeated authenticator verification failures", Severities.High, true),
            EventTypes.RecoveryCooldown => new("recovery-cooldown", "Repeated recovery-code failures", Severities.High, true),
            EventTypes.RecoveryCodeSucceeded => new("recovery-used", "Recovery code used to sign in", Severities.High, false),
            EventTypes.MfaResetApproved => new("mfa-reset", "Authenticator reset approved", Severities.High, false),
            EventTypes.RoleChanged => new("role-change", "Role changed", Severities.High, false),
            EventTypes.AccountCreated when ContainsRole(auditEvent.Details, Security.Roles.Administrator)
                => new("admin-created", "Administrator account created", Severities.Critical, false),
            EventTypes.AccountDisabled => new("account-disabled", "Account disabled", Severities.High, false),
            EventTypes.SecuritySettingsChanged => new("settings-changed", "Security settings changed", Severities.High, false),
            EventTypes.AccessDenied => new("access-denied", "Unauthorized access attempt", Severities.High, true),
            EventTypes.SessionRejected => new("suspicious-session", "Rejected session presented", Severities.High, true),
            EventTypes.IpRateLimited => new("ip-rate-limit", "Authentication rate limit triggered", Severities.Medium, true),
            _ => null
        };
    }

    public static bool ContainsRole(string? details, string role) =>
        (details ?? "").Contains($"Role={role}", StringComparison.Ordinal);
}
