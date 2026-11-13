namespace SecureGate1.Services;

public sealed class MfaResetListItem
{
    public long MfaResetRequestId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string Status { get; init; } = "";
    public string VerificationMethod { get; init; } = "";
    public string Notes { get; init; } = "";
    public string? DecisionNotes { get; init; }
    public string TargetUserId { get; init; } = "";
    public string TargetDisplayName { get; init; } = "";
    public string? TargetEmail { get; init; }
    public string TargetRole { get; init; } = "";
    public string RequesterUserId { get; init; } = "";
    public string RequesterDisplayName { get; init; } = "";
    public string? RequesterEmail { get; init; }
}
