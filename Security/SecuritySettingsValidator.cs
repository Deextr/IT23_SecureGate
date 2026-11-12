using SecureGate1.Models;

namespace SecureGate1.Security;

public static class SecuritySettingsValidator
{
    public static IReadOnlyList<string> Validate(SecuritySettings settings)
    {
        var errors = new List<string>();
        if (settings.LockoutFailedAttempts is < 3 or > 20)
            errors.Add("Password lockout threshold must be between 3 and 20.");
        if (settings.LockoutMinutes is < 1 or > 1440)
            errors.Add("Password lockout length must be between 1 and 1440 minutes.");
        if (settings.TotpFailureLimit is < 3 or > 20)
            errors.Add("TOTP failure limit must be between 3 and 20.");
        if (settings.TotpFailureWindowMinutes is < 1 or > 60)
            errors.Add("TOTP failure window must be between 1 and 60 minutes.");
        if (settings.TotpCooldownMinutes is < 1 or > 1440)
            errors.Add("TOTP cooldown must be between 1 and 1440 minutes.");
        if (settings.RecoveryFailureLimit is < 3 or > 20)
            errors.Add("Recovery failure limit must be between 3 and 20.");
        if (settings.RecoveryFailureWindowMinutes is < 1 or > 60)
            errors.Add("Recovery failure window must be between 1 and 60 minutes.");
        if (settings.RecoveryCooldownMinutes is < 1 or > 1440)
            errors.Add("Recovery cooldown must be between 1 and 1440 minutes.");
        if (settings.StepUpMinutes is < 1 or > 30)
            errors.Add("Step-up window must be between 1 and 30 minutes.");
        if (settings.AuditRetentionDays is < 365 or > 3650)
            errors.Add("Audit retention must be at least 365 days and at most 3650 days.");
        return errors;
    }
}
