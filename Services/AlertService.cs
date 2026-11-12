using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;

namespace SecureGate1.Services;

public class AlertService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly TimeProvider _time;

    public AlertService(IDbContextFactory<ApplicationDbContext> factory, TimeProvider time)
    {
        _factory = factory;
        _time = time;
    }

    public async Task RaiseForAsync(AuthenticationAuditLog auditEvent, CancellationToken cancellationToken = default)
    {
        var draft = AlertRules.FromEvent(auditEvent);
        if (draft is null)
            return;

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        if (draft.DedupeWhileOpen)
        {
            var open = db.SecurityAlerts.Where(a =>
                a.RuleKey == draft.RuleKey && a.Status != AlertStatuses.Resolved);

            open = auditEvent.TargetUserId is not null
                ? open.Where(a => a.TargetUserId == auditEvent.TargetUserId)
                : open.Where(a => a.TargetUserId == null && a.SourceIp == auditEvent.IpAddress);

            if (await open.AnyAsync(cancellationToken))
                return;
        }

        db.SecurityAlerts.Add(new SecurityAlert
        {
            RuleKey = draft.RuleKey,
            Title = draft.Title,
            Severity = draft.Severity,
            Status = AlertStatuses.Open,
            RelatedEventId = auditEvent.EventId,
            TargetUserId = auditEvent.TargetUserId,
            SourceIp = auditEvent.IpAddress,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ServiceResult> AcknowledgeAsync(long alertId, string officerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var alert = await db.SecurityAlerts.SingleOrDefaultAsync(a => a.AlertId == alertId, cancellationToken);
        if (alert is null)
            return ServiceResult.Fail("That alert no longer exists.");
        if (alert.Status == AlertStatuses.Resolved)
            return ServiceResult.Fail("Resolved alerts cannot be acknowledged.");

        alert.Status = AlertStatuses.Acknowledged;
        alert.AcknowledgedAtUtc = _time.GetUtcNow().UtcDateTime;
        alert.AcknowledgedById = officerId;
        alert.ResponsibleOfficerId = officerId;
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok("Alert acknowledged.");
    }

    public async Task<ServiceResult> ResolveAsync(long alertId, string officerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var alert = await db.SecurityAlerts.SingleOrDefaultAsync(a => a.AlertId == alertId, cancellationToken);
        if (alert is null)
            return ServiceResult.Fail("That alert no longer exists.");
        if (alert.Status == AlertStatuses.Resolved)
            return ServiceResult.Ok("Alert is already resolved.");

        alert.Status = AlertStatuses.Resolved;
        alert.ResolvedAtUtc = _time.GetUtcNow().UtcDateTime;
        alert.ResolvedById = officerId;
        alert.ResponsibleOfficerId ??= officerId;
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok("Alert resolved.");
    }
}
