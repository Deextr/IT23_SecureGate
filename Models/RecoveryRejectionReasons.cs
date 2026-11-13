namespace SecureGate1.Models;

public static class RecoveryRejectionReasons
{
    public const string InsufficientIdentityProof = "InsufficientIdentityProof";
    public const string IncompleteRequest = "IncompleteRequest";
    public const string DoesNotMeetPolicy = "DoesNotMeetPolicy";
    public const string DuplicateOrUnnecessary = "DuplicateOrUnnecessary";
    public const string EmployeeWithdrew = "EmployeeWithdrew";
    public const string Other = "Other";

    public static readonly IReadOnlyList<(string Key, string Label)> Options =
    [
        (InsufficientIdentityProof, "Identity verification was insufficient"),
        (IncompleteRequest, "Request details were incomplete"),
        (DoesNotMeetPolicy, "Does not meet organization recovery policy"),
        (DuplicateOrUnnecessary, "Duplicate or unnecessary request"),
        (EmployeeWithdrew, "Employee withdrew the request"),
        (Other, "Other")
    ];

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        InsufficientIdentityProof,
        IncompleteRequest,
        DoesNotMeetPolicy,
        DuplicateOrUnnecessary,
        EmployeeWithdrew,
        Other
    };

    public static string Label(string key) => key switch
    {
        InsufficientIdentityProof => "Identity verification was insufficient",
        IncompleteRequest => "Request details were incomplete",
        DoesNotMeetPolicy => "Does not meet organization recovery policy",
        DuplicateOrUnnecessary => "Duplicate or unnecessary request",
        EmployeeWithdrew => "Employee withdrew the request",
        Other => "Other",
        _ => key
    };
}
