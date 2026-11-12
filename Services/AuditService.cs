using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;

namespace SecureGate1.Services;

public class AuditService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly AlertService _alerts;
    private readonly IHttpContextAccessor _http;
    private readonly TimeProvider _time;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IDbContextFactory<ApplicationDbContext> factory,
        AlertService alerts,
        IHttpContextAccessor http,
        TimeProvider time,
        ILogger<AuditService> logger)
    {
        _factory = factory;
        _alerts = alerts;
        _http = http;
        _time = time;
        _logger = logger;
    }

    public async Task<long> WriteAsync(AuditWrite write, CancellationToken cancellationToken = default)
    {
        if (!EventTypes.All.Contains(write.EventType))
            throw new ArgumentException("Unknown audit event type.", nameof(write));

        var http = _http.HttpContext;
        var entry = new AuthenticationAuditLog
        {
            OccurredAtUtc = _time.GetUtcNow().UtcDateTime,
            EventType = write.EventType,
            Outcome = Truncate(write.Outcome, 20) ?? Outcomes.Failure,
            Severity = Truncate(write.Severity, 20) ?? Severities.Info,
            ActorUserId = write.ActorUserId,
            TargetUserId = write.TargetUserId,
            SubjectIdentifier = Clean(write.SubjectIdentifier, 50),
            IpAddress = Clean(write.IpAddress ?? http?.Connection.RemoteIpAddress?.ToString(), 45),
            UserAgent = Clean(http?.Request.Headers.UserAgent.ToString(), 512),
            CorrelationId = Clean(http?.TraceIdentifier, 64),
            Details = Clean(write.Details, 1000)
        };

        await using (var db = await _factory.CreateDbContextAsync(cancellationToken))
        {
            db.AuthenticationAuditLogs.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }

        try
        {
            await _alerts.RaiseForAsync(entry, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Alert creation failed for audit event {EventId}.", entry.EventId);
        }

        return entry.EventId;
    }

    public static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var filtered = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (filtered.Length == 0)
            return null;
        return filtered.Length <= max ? filtered : filtered[..max];
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);
}
