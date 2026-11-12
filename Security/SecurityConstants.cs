namespace SecureGate1.Security;

public static class Roles
{
    public const string Superadmin = "Superadmin";
    public const string Administrator = "Administrator";
    public const string SecurityOfficer = "SecurityOfficer";
    public const string Employee = "Employee";

    /// <summary>Roles that may be assigned through account management in the web application.</summary>
    public static readonly IReadOnlySet<string> Assignable = new HashSet<string>(StringComparer.Ordinal)
    {
        Administrator,
        SecurityOfficer,
        Employee
    };

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Superadmin,
        Administrator,
        SecurityOfficer,
        Employee
    };

    public static readonly IReadOnlySet<string> AccountAdministrators = new HashSet<string>(StringComparer.Ordinal)
    {
        Superadmin,
        Administrator
    };

    public static bool IsAssignable(string? role) =>
        !string.IsNullOrEmpty(role) && Assignable.Contains(role);

    public static bool IsAccountAdministrator(string? role) =>
        !string.IsNullOrEmpty(role) && AccountAdministrators.Contains(role);

    public static string DisplayName(string role) => role switch
    {
        Superadmin => "Superadmin",
        Administrator => "Administrator",
        SecurityOfficer => "Security Officer",
        Employee => "Employee",
        _ => role
    };
}

public static class AuthSchemes
{
    public const string PreAuth = "SecureGate.PreAuth";
}

public static class AppClaims
{
    public const string Amr = "amr";
    public const string Mfa = "mfa";
    public const string AuthTime = "auth_time";
    public const string MfaTime = "mfa_time";
    public const string PreAuthStage = "preauth_stage";
}

public static class PreAuthStages
{
    public const string ChangePassword = "change-password";
    public const string Enroll = "enroll";
    public const string Verify = "verify";
    public const string ShowRecoveryCodes = "show-codes";
}

public static class Policies
{
    public const string AdministratorOnly = "AdministratorOnly";
    public const string MonitoringAccess = "MonitoringAccess";
}

public static class PreAuthItems
{
    public const string UserId = "SecureGate.PreAuthUserId";
}

public static class VerificationMethods
{
    public const string InPerson = "InPerson";
    public const string OrganizationRecord = "OrganizationRecord";
    public const string ManagerConfirmation = "ManagerConfirmation";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        InPerson,
        OrganizationRecord,
        ManagerConfirmation
    };

    public static string DisplayName(string method) => method switch
    {
        InPerson => "In person",
        OrganizationRecord => "Organization record",
        ManagerConfirmation => "Manager confirmation",
        _ => method
    };
}
