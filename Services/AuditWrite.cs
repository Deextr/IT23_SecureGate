namespace SecureGate1.Services;

public sealed class AuditWrite
{
    public required string EventType { get; init; }
    public required string Outcome { get; init; }
    public string Severity { get; init; } = Models.Severities.Info;
    public string? ActorUserId { get; init; }
    public string? TargetUserId { get; init; }
    public string? SubjectIdentifier { get; init; }
    public string? Details { get; init; }
    public string? IpAddress { get; init; }
}
