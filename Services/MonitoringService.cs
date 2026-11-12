using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;

namespace SecureGate1.Services;

public sealed class AuditQuery
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public string? EventType { get; init; }
    public string? Outcome { get; init; }
    public string? Account { get; init; }
    public string? Severity { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed class AuditPage
{
    public IReadOnlyList<AuthenticationAuditLog> Items { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public class MonitoringService
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly SecurityPolicyOptions _policy;
    private readonly TimeProvider _time;

    public MonitoringService(ApplicationDbContext db, AuditService audit, IOptions<SecurityPolicyOptions> policy, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _policy = policy.Value;
        _time = time;
    }

    public string? ValidateRange(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc <= fromUtc)
            return "The end date must be after the start date.";
        if ((toUtc - fromUtc).TotalDays > _policy.MaxAuditRangeDays)
            return $"Choose a range of {_policy.MaxAuditRangeDays} days or fewer.";
        return null;
    }

    public async Task<AuditPage> SearchAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, _policy.MaxPageSize);
        var page = Math.Max(1, query.Page);
        var filtered = ApplyFilters(_db.AuthenticationAuditLogs.AsNoTracking(), query);
        var total = await filtered.CountAsync(cancellationToken);
        var items = await filtered
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.EventId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new AuditPage { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<byte[]> ExportCsvAsync(AuditQuery query, string actorId, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(_db.AuthenticationAuditLogs.AsNoTracking(), query);
        var rows = await filtered
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.EventId)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("EventId,OccurredAtUtc,EventType,Outcome,Severity,ActorUserId,TargetUserId,SubjectIdentifier,IpAddress,CorrelationId,Details");
        foreach (var row in rows)
        {
            builder.Append(CsvFormatter.Cell(row.EventId.ToString(CultureInfo.InvariantCulture))).Append(',')
                .Append(CsvFormatter.Cell(row.OccurredAtUtc.ToString("u", CultureInfo.InvariantCulture))).Append(',')
                .Append(CsvFormatter.Cell(row.EventType)).Append(',')
                .Append(CsvFormatter.Cell(row.Outcome)).Append(',')
                .Append(CsvFormatter.Cell(row.Severity)).Append(',')
                .Append(CsvFormatter.Cell(row.ActorUserId)).Append(',')
                .Append(CsvFormatter.Cell(row.TargetUserId)).Append(',')
                .Append(CsvFormatter.Cell(row.SubjectIdentifier)).Append(',')
                .Append(CsvFormatter.Cell(row.IpAddress)).Append(',')
                .Append(CsvFormatter.Cell(row.CorrelationId)).Append(',')
                .Append(CsvFormatter.Cell(row.Details))
                .AppendLine();
        }

        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.ReportExported,
            Outcome = Outcomes.Success,
            Severity = Severities.Info,
            ActorUserId = actorId,
            TargetUserId = actorId,
            Details = $"Rows={rows.Count}; From={query.FromUtc:u}; To={query.ToUtc:u}"
        }, cancellationToken);
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public Task<AuthenticationAuditLog?> FindEventAsync(long id, CancellationToken cancellationToken = default) =>
        _db.AuthenticationAuditLogs.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == id, cancellationToken);

    public async Task<ServiceResult> SaveInvestigationAsync(long alertId, string officerId, string status, string notes, CancellationToken cancellationToken = default)
    {
        if (!InvestigationStatuses.All.Contains(status))
            return ServiceResult.Fail("Choose a valid investigation status.");
        var cleanNotes = AuditService.Clean(notes, 2000) ?? "";
        if (cleanNotes.Length > 2000)
            return ServiceResult.Fail("Notes must be 2000 characters or fewer.");

        var alert = await _db.SecurityAlerts.SingleOrDefaultAsync(a => a.AlertId == alertId, cancellationToken);
        if (alert is null)
            return ServiceResult.Fail("That alert was not found.");

        var record = await _db.InvestigationRecords.SingleOrDefaultAsync(i => i.AlertId == alertId, cancellationToken);
        var now = _time.GetUtcNow().UtcDateTime;
        if (record is null)
        {
            record = new InvestigationRecord
            {
                AlertId = alertId,
                AssignedOfficerId = officerId,
                CreatedAtUtc = now
            };
            _db.InvestigationRecords.Add(record);
        }

        record.Status = status;
        record.Notes = cleanNotes;
        record.UpdatedAtUtc = now;
        record.ClosedAtUtc = status == InvestigationStatuses.Closed ? now : null;
        record.AssignedOfficerId = officerId;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.InvestigationUpdated,
            Outcome = Outcomes.Success,
            ActorUserId = officerId,
            TargetUserId = alert.TargetUserId,
            Details = $"Alert={alertId}; Status={status}"
        }, cancellationToken);
        return ServiceResult.Ok("Investigation saved.");
    }

    public async Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var startOfDay = _time.GetUtcNow().UtcDateTime.Date;
        return new DashboardSummary
        {
            ActiveUsers = await _db.Users.CountAsync(u => u.IsActive, cancellationToken),
            DisabledUsers = await _db.Users.CountAsync(u => !u.IsActive, cancellationToken),
            OpenAlerts = await _db.SecurityAlerts.CountAsync(a => a.Status != AlertStatuses.Resolved, cancellationToken),
            FailedPasswordsToday = await _db.AuthenticationAuditLogs.CountAsync(
                e => e.EventType == EventTypes.PasswordFailed && e.OccurredAtUtc >= startOfDay, cancellationToken),
            PendingResets = await _db.MfaResetRequests.CountAsync(r => r.Status == ResetRequestStatuses.Pending, cancellationToken),
            RecentEvents = await _db.AuthenticationAuditLogs.AsNoTracking()
                .OrderByDescending(e => e.EventId).Take(8).ToListAsync(cancellationToken),
            OpenAlertList = await _db.SecurityAlerts.AsNoTracking()
                .Where(a => a.Status != AlertStatuses.Resolved)
                .OrderByDescending(a => a.AlertId).Take(6).ToListAsync(cancellationToken)
        };
    }

    private IQueryable<AuthenticationAuditLog> ApplyFilters(IQueryable<AuthenticationAuditLog> query, AuditQuery input)
    {
        query = query.Where(e => e.OccurredAtUtc >= input.FromUtc && e.OccurredAtUtc < input.ToUtc);
        if (!string.IsNullOrWhiteSpace(input.EventType) && EventTypes.All.Contains(input.EventType))
            query = query.Where(e => e.EventType == input.EventType);
        if (!string.IsNullOrWhiteSpace(input.Outcome) && input.Outcome is Outcomes.Success or Outcomes.Failure or Outcomes.Denied)
            query = query.Where(e => e.Outcome == input.Outcome);
        if (!string.IsNullOrWhiteSpace(input.Severity) &&
            (input.Severity == Severities.Info || Severities.AlertLevels.Contains(input.Severity)))
            query = query.Where(e => e.Severity == input.Severity);
        if (!string.IsNullOrWhiteSpace(input.Account))
        {
            var term = input.Account.Trim();
            if (term.Length > 254)
                term = term[..254];
            query = query.Where(e =>
                e.SubjectIdentifier == term ||
                _db.Users.Any(u =>
                    (u.Email == term || u.DisplayName.Contains(term) || u.Id == term) &&
                    (u.Id == e.ActorUserId || u.Id == e.TargetUserId)));
        }

        return query;
    }
}

public static class CsvFormatter
{
    public static string Cell(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0]))
            text = "'" + text;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            text = "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return text;
    }
}

public sealed class DashboardSummary
{
    public int ActiveUsers { get; init; }
    public int DisabledUsers { get; init; }
    public int OpenAlerts { get; init; }
    public int FailedPasswordsToday { get; init; }
    public int PendingResets { get; init; }
    public IReadOnlyList<AuthenticationAuditLog> RecentEvents { get; init; } = [];
    public IReadOnlyList<SecurityAlert> OpenAlertList { get; init; } = [];
}
