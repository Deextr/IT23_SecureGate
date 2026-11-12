namespace SecureGate1.Models;

public class AuthenticationAuditLog
{
    public long EventId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string EventType { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string Severity { get; set; } = Severities.Info;
    public string? ActorUserId { get; set; }
    public string? TargetUserId { get; set; }
    public string? SubjectIdentifier { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? Details { get; set; }
}
