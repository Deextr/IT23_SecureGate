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
