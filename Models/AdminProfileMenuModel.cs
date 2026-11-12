namespace SecureGate1.Models;

public class AdminProfileMenuModel
{
    public string DisplayName { get; init; } = "";
    public string Email { get; init; } = "";
    public string RoleLabel { get; init; } = "";
    public string Initials { get; init; } = "?";
    public string? PhotoUrl { get; init; }
    public bool HasPhoto { get; init; }
    public bool MfaEnrolled { get; init; }
    public int UnusedRecoveryCodes { get; init; }
    public string ReturnUrl { get; init; } = "/";
}
