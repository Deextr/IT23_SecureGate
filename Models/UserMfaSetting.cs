namespace SecureGate1.Models;

public class UserMfaSetting
{
    public string UserId { get; set; } = "";
    public string? ProtectedSecret { get; set; }
    public bool IsEnabled { get; set; }
    public int KeyVersion { get; set; } = 1;
    public DateTime? EnrolledAtUtc { get; set; }
    public DateTime? LastVerifiedAtUtc { get; set; }
    public long? LastAcceptedTimeStep { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
