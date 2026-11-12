namespace SecureGate1.Models;

public class SecurityAlert
{
    public long AlertId { get; set; }
    public string RuleKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Severity { get; set; } = Severities.High;
    public string Status { get; set; } = AlertStatuses.Open;
    public long? RelatedEventId { get; set; }
    public string? TargetUserId { get; set; }
    public string? SourceIp { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedById { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolvedById { get; set; }
    public string? ResponsibleOfficerId { get; set; }
    public AuthenticationAuditLog? RelatedEvent { get; set; }
    public InvestigationRecord? Investigation { get; set; }
}
