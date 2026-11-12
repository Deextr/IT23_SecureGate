using Microsoft.AspNetCore.Identity;

namespace SecureGate1.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public string? ProfilePhotoPath { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
    public DateTime? LastSignInAtUtc { get; set; }
}
