namespace SecureGate1.Models;

public class UserRecoveryCode
{
    public long RecoveryCodeId { get; set; }
    public string UserId { get; set; } = "";
    public Guid BatchId { get; set; }
    public byte[] Salt { get; set; } = [];
    public byte[] CodeHash { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
