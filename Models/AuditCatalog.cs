namespace SecureGate1.Models;

public static class EventTypes
{
    public const string PasswordFailed = "PasswordFailed";
    public const string PasswordAccepted = "PasswordAccepted";
    public const string AccountLocked = "AccountLocked";
    public const string SignInRejectedLocked = "SignInRejectedLocked";
    public const string SignInRejectedDisabled = "SignInRejectedDisabled";
    public const string TotpSucceeded = "TotpSucceeded";
    public const string TotpFailed = "TotpFailed";
    public const string TotpCooldown = "TotpCooldown";
    public const string RecoveryCodeSucceeded = "RecoveryCodeSucceeded";
    public const string RecoveryCodeFailed = "RecoveryCodeFailed";
    public const string RecoveryCooldown = "RecoveryCooldown";
    public const string RecoveryCodesGenerated = "RecoveryCodesGenerated";
    public const string RecoveryCodesRevoked = "RecoveryCodesRevoked";
    public const string MfaEnrollmentConfirmed = "MfaEnrollmentConfirmed";
    public const string MfaEnrollmentFailed = "MfaEnrollmentFailed";
    public const string MfaResetRequested = "MfaResetRequested";
    public const string MfaResetApproved = "MfaResetApproved";
    public const string MfaResetRejected = "MfaResetRejected";
    public const string Logout = "Logout";
    public const string SessionAbsoluteExpired = "SessionAbsoluteExpired";
    public const string SessionRejected = "SessionRejected";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordResetByAdmin = "PasswordResetByAdmin";
    public const string RoleChanged = "RoleChanged";
    public const string AccountCreated = "AccountCreated";
    public const string AccountDisabled = "AccountDisabled";
    public const string AccountReactivated = "AccountReactivated";
    public const string SecuritySettingsChanged = "SecuritySettingsChanged";
    public const string ReportExported = "ReportExported";
    public const string InvestigationUpdated = "InvestigationUpdated";
    public const string AlertAcknowledged = "AlertAcknowledged";
    public const string AlertResolved = "AlertResolved";
    public const string AccessDenied = "AccessDenied";
    public const string IpRateLimited = "IpRateLimited";
    public const string ProfileUpdated = "ProfileUpdated";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PasswordFailed, PasswordAccepted, AccountLocked, SignInRejectedLocked, SignInRejectedDisabled,
        TotpSucceeded, TotpFailed, TotpCooldown, RecoveryCodeSucceeded, RecoveryCodeFailed, RecoveryCooldown,
        RecoveryCodesGenerated, RecoveryCodesRevoked, MfaEnrollmentConfirmed, MfaEnrollmentFailed,
        MfaResetRequested, MfaResetApproved, MfaResetRejected, Logout, SessionAbsoluteExpired, SessionRejected,
        PasswordChanged, PasswordResetByAdmin, RoleChanged, AccountCreated, AccountDisabled, AccountReactivated,
        SecuritySettingsChanged, ReportExported, InvestigationUpdated, AlertAcknowledged, AlertResolved,
        AccessDenied, IpRateLimited, ProfileUpdated
    };

    public static string Label(string eventType) => eventType switch
    {
        PasswordFailed => "Password failed",
        PasswordAccepted => "Password accepted",
        AccountLocked => "Account locked",
        SignInRejectedLocked => "Sign-in rejected (locked)",
        SignInRejectedDisabled => "Sign-in rejected (disabled)",
        TotpSucceeded => "TOTP succeeded",
        TotpFailed => "TOTP failed",
        TotpCooldown => "TOTP cooldown",
        RecoveryCodeSucceeded => "Recovery code succeeded",
        RecoveryCodeFailed => "Recovery code failed",
        RecoveryCooldown => "Recovery cooldown",
        RecoveryCodesGenerated => "Recovery codes generated",
        RecoveryCodesRevoked => "Recovery codes revoked",
        MfaEnrollmentConfirmed => "MFA enrollment confirmed",
        MfaEnrollmentFailed => "MFA enrollment failed",
        MfaResetRequested => "MFA reset requested",
        MfaResetApproved => "MFA reset approved",
        MfaResetRejected => "MFA reset rejected",
        Logout => "Logout",
        SessionAbsoluteExpired => "Session expired",
        SessionRejected => "Session rejected",
        PasswordChanged => "Password changed",
        PasswordResetByAdmin => "Password reset by admin",
        RoleChanged => "Role changed",
        AccountCreated => "Account created",
        AccountDisabled => "Account disabled",
        AccountReactivated => "Account reactivated",
        SecuritySettingsChanged => "Security settings changed",
        ReportExported => "Report exported",
        InvestigationUpdated => "Investigation updated",
        AlertAcknowledged => "Alert acknowledged",
        AlertResolved => "Alert resolved",
        AccessDenied => "Access denied",
        IpRateLimited => "IP rate limited",
        ProfileUpdated => "Profile updated",
        _ => eventType
    };
}

public static class AuditModules
{
    public const string Authentication = "Authentication";
    public const string Mfa = "MFA";
    public const string Accounts = "Accounts";
    public const string Sessions = "Sessions";
    public const string Security = "Security";
    public const string Monitoring = "Monitoring";

    public static readonly IReadOnlyList<string> All =
    [
        Authentication, Mfa, Accounts, Sessions, Security, Monitoring
    ];

    private static readonly IReadOnlyDictionary<string, string> ByEvent = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [EventTypes.PasswordFailed] = Authentication,
        [EventTypes.PasswordAccepted] = Authentication,
        [EventTypes.AccountLocked] = Authentication,
        [EventTypes.SignInRejectedLocked] = Authentication,
        [EventTypes.SignInRejectedDisabled] = Authentication,
        [EventTypes.TotpSucceeded] = Mfa,
        [EventTypes.TotpFailed] = Mfa,
        [EventTypes.TotpCooldown] = Mfa,
        [EventTypes.RecoveryCodeSucceeded] = Mfa,
        [EventTypes.RecoveryCodeFailed] = Mfa,
        [EventTypes.RecoveryCooldown] = Mfa,
        [EventTypes.RecoveryCodesGenerated] = Mfa,
        [EventTypes.RecoveryCodesRevoked] = Mfa,
        [EventTypes.MfaEnrollmentConfirmed] = Mfa,
        [EventTypes.MfaEnrollmentFailed] = Mfa,
        [EventTypes.MfaResetRequested] = Mfa,
        [EventTypes.MfaResetApproved] = Mfa,
        [EventTypes.MfaResetRejected] = Mfa,
        [EventTypes.Logout] = Sessions,
        [EventTypes.SessionAbsoluteExpired] = Sessions,
        [EventTypes.SessionRejected] = Sessions,
        [EventTypes.PasswordChanged] = Accounts,
        [EventTypes.PasswordResetByAdmin] = Accounts,
        [EventTypes.RoleChanged] = Accounts,
        [EventTypes.AccountCreated] = Accounts,
        [EventTypes.AccountDisabled] = Accounts,
        [EventTypes.AccountReactivated] = Accounts,
        [EventTypes.ProfileUpdated] = Accounts,
        [EventTypes.SecuritySettingsChanged] = Security,
        [EventTypes.AccessDenied] = Security,
        [EventTypes.IpRateLimited] = Security,
        [EventTypes.ReportExported] = Monitoring,
        [EventTypes.InvestigationUpdated] = Monitoring,
        [EventTypes.AlertAcknowledged] = Monitoring,
        [EventTypes.AlertResolved] = Monitoring,
    };

    public static string GetModule(string eventType) =>
        ByEvent.TryGetValue(eventType, out var module) ? module : "Other";

    public static IReadOnlyList<string> EventTypesForModule(string? module)
    {
        if (string.IsNullOrWhiteSpace(module) || !All.Contains(module))
            return [];
        return ByEvent.Where(pair => pair.Value == module).Select(pair => pair.Key).ToList();
    }

    public static IReadOnlyList<string> EventTypesMatchingModuleSearch(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return [];
        var lowered = term.Trim();
        return ByEvent
            .Where(pair => pair.Value.Contains(lowered, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}

public static class Outcomes
{
    public const string Success = "Success";
    public const string Failure = "Failure";
    public const string Denied = "Denied";
}

public static class Severities
{
    public const string Info = "Info";
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public static readonly IReadOnlySet<string> AlertLevels = new HashSet<string>(StringComparer.Ordinal)
    {
        Low, Medium, High, Critical
    };
}

public static class AlertStatuses
{
    public const string Open = "Open";
    public const string Acknowledged = "Acknowledged";
    public const string Resolved = "Resolved";
}

public static class InvestigationStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string Closed = "Closed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Open, InProgress, Closed
    };
}

public static class ResetRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
