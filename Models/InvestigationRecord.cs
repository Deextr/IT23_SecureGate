namespace SecureGate1.Models;

public class InvestigationRecord
{
    public long InvestigationId { get; set; }
    public long AlertId { get; set; }
    public string AssignedOfficerId { get; set; } = "";
    public string Status { get; set; } = InvestigationStatuses.Open;
    public string Notes { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public SecurityAlert Alert { get; set; } = null!;
}
