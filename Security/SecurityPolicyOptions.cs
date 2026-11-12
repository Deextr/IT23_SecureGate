namespace SecureGate1.Security;

public class SecurityPolicyOptions
{
    public const string SectionName = "SecurityPolicy";

    public int PasswordMinLength { get; set; } = 12;
    public int PasswordMaxLength { get; set; } = 128;
    public int SessionIdleMinutes { get; set; } = 15;
    public int SessionAbsoluteHours { get; set; } = 8;
    public int PreAuthMinutes { get; set; } = 10;
    public int IpPermitLimit { get; set; } = 30;
    public int IpWindowMinutes { get; set; } = 5;
    public int TotpDigits { get; set; } = 6;
    public int TotpPeriodSeconds { get; set; } = 30;
    public int TotpDriftSteps { get; set; } = 1;
    public int RecoveryCodeCount { get; set; } = 10;
    public int PageSize { get; set; } = 25;
    public int MaxPageSize { get; set; } = 100;
    public int MaxRequestBodyBytes { get; set; } = 65536;
    public int MaxAuditRangeDays { get; set; } = 366;

    public static IReadOnlyList<string> Validate(SecurityPolicyOptions policy)
    {
        var errors = new List<string>();
        if (policy.PasswordMinLength < 12)
            errors.Add("Password minimum length must be at least 12.");
        if (policy.PasswordMaxLength < policy.PasswordMinLength || policy.PasswordMaxLength > 256)
            errors.Add("Password maximum length must be between the minimum and 256.");
        if (policy.SessionIdleMinutes is < 5 or > 60)
            errors.Add("Session idle timeout must be between 5 and 60 minutes.");
        if (policy.SessionAbsoluteHours is < 1 or > 24)
            errors.Add("Absolute session lifetime must be between 1 and 24 hours.");
        if (policy.PreAuthMinutes is < 5 or > 30)
            errors.Add("Pre-authentication lifetime must be between 5 and 30 minutes.");
        if (policy.IpPermitLimit is < 5 or > 300)
            errors.Add("IP permit limit must be between 5 and 300.");
        if (policy.IpWindowMinutes is < 1 or > 60)
            errors.Add("IP window must be between 1 and 60 minutes.");
        if (policy.TotpDigits != 6 || policy.TotpPeriodSeconds != 30)
            errors.Add("TOTP must use 6 digits and a 30-second period.");
        if (policy.TotpDriftSteps is < 0 or > 1)
            errors.Add("TOTP clock drift must be 0 or 1 step.");
        if (policy.RecoveryCodeCount is < 8 or > 20)
            errors.Add("Recovery code count must be between 8 and 20.");
        if (policy.PageSize is < 10 or > 100 || policy.MaxPageSize < policy.PageSize || policy.MaxPageSize > 100)
            errors.Add("Page size must be between 10 and 100, and the maximum cannot exceed 100.");
        if (policy.MaxRequestBodyBytes is < 1024 or > 1_048_576)
            errors.Add("Maximum request body must be between 1 KB and 1 MB.");
        if (policy.MaxAuditRangeDays is < 1 or > 366)
            errors.Add("Audit search range must be between 1 and 366 days.");
        return errors;
    }
}
