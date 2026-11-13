namespace SecureGate1.Models;

public static class RecoveryVerificationChecks
{
    public const string IdentityVerified = "IdentityVerified";
    public const string NameAndIdConfirmed = "NameAndIdConfirmed";
    public const string DepartmentConfirmed = "DepartmentConfirmed";
    public const string OrganizationRecordVerified = "OrganizationRecordVerified";
    public const string AccessIssueConfirmed = "AccessIssueConfirmed";
    public const string Other = "Other";

    public static readonly IReadOnlyList<(string Key, string Label, bool Required)> Options =
    [
        (IdentityVerified, "Verified the employee's identity", false),
        (NameAndIdConfirmed, "Confirmed the employee's name and account ID", false),
        (DepartmentConfirmed, "Confirmed department or assigned office", false),
        (OrganizationRecordVerified, "Verified identity through an authorized company record", false),
        (AccessIssueConfirmed, "Confirmed the reported account access issue", false),
        (Other, "Other (short explanation required)", false)
    ];

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        IdentityVerified,
        NameAndIdConfirmed,
        DepartmentConfirmed,
        OrganizationRecordVerified,
        AccessIssueConfirmed,
        Other
    };

    public static string Label(string key) => key switch
    {
        IdentityVerified => "Verified the employee's identity",
        NameAndIdConfirmed => "Confirmed the employee's name and account ID",
        DepartmentConfirmed => "Confirmed department or assigned office",
        OrganizationRecordVerified => "Verified identity through an authorized company record",
        AccessIssueConfirmed => "Confirmed the reported account access issue",
        Other => "Other",
        _ => key
    };
}
