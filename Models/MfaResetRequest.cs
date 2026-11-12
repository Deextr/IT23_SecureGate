namespace SecureGate1.Models;

public class MfaResetRequest
{
    public long MfaResetRequestId { get; set; }
    public string TargetUserId { get; set; } = "";
    public string RequesterUserId { get; set; } = "";
    public string VerificationMethod { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = ResetRequestStatuses.Pending;
    public string? ReviewerUserId { get; set; }
    public string? DecisionNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public ApplicationUser TargetUser { get; set; } = null!;
    public ApplicationUser RequesterUser { get; set; } = null!;
}
