using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;

namespace SecureGate1.Services;

public sealed class AuditQuery
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public string? Search { get; init; }
    public string? EventType { get; init; }
    public string? Outcome { get; init; }
    public string? AccountUserId { get; init; }
    public string? Role { get; init; }
    public string? Module { get; init; }
    public string? Severity { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed class AuditPage
{
    public IReadOnlyList<AuditListItem> Items { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public class MonitoringService
{
    private const int ExportRowLimit = 5000;
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
        var events = await filtered
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.EventId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var users = await LoadUserSnapshotsAsync(
            events.SelectMany(e => new[] { e.ActorUserId, e.TargetUserId }),
            cancellationToken);
        var items = events.Select(e => MapListItem(e, users)).ToList();
        return new AuditPage { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<AuditAccountFilterOption>> GetAccountFilterOptionsAsync(CancellationToken cancellationToken = default) =>
        await _db.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .Select(u => new AuditAccountFilterOption
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Email = u.Email
            })
            .Take(500)
            .ToListAsync(cancellationToken);

    public async Task<byte[]> ExportCsvAsync(AuditQuery query, string actorId, CancellationToken cancellationToken = default)
    {
        var rows = await LoadExportRowsAsync(query, cancellationToken);
        var users = await LoadUserSnapshotsAsync(
            rows.SelectMany(e => new[] { e.ActorUserId, e.TargetUserId }),
            cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("OccurredAtUtc,AccountName,AccountEmail,Role,Action,Module,Outcome,Severity,Description,EventId,IpAddress,CorrelationId");
        foreach (var row in rows)
        {
            var account = AuditAccountResolver.ResolveForList(row, users);
            builder.Append(CsvFormatter.Cell(row.OccurredAtUtc.ToString("u", CultureInfo.InvariantCulture))).Append(',')
                .Append(CsvFormatter.Cell(account.PrimaryName)).Append(',')
                .Append(CsvFormatter.Cell(account.SecondaryLine)).Append(',')
                .Append(CsvFormatter.Cell(account.RoleLabel)).Append(',')
                .Append(CsvFormatter.Cell(EventTypes.Label(row.EventType))).Append(',')
                .Append(CsvFormatter.Cell(AuditModules.GetModule(row.EventType))).Append(',')
                .Append(CsvFormatter.Cell(row.Outcome)).Append(',')
                .Append(CsvFormatter.Cell(row.Severity)).Append(',')
                .Append(CsvFormatter.Cell(row.Details)).Append(',')
                .Append(CsvFormatter.Cell(row.EventId.ToString(CultureInfo.InvariantCulture))).Append(',')
                .Append(CsvFormatter.Cell(row.IpAddress)).Append(',')
                .Append(CsvFormatter.Cell(row.CorrelationId))
                .AppendLine();
        }

        await WriteExportAuditAsync(query, rows.Count, "CSV", actorId, cancellationToken);
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public async Task<byte[]> ExportPdfAsync(AuditQuery query, string actorId, CancellationToken cancellationToken = default)
    {
        var rows = await LoadExportRowsAsync(query, cancellationToken);
        var users = await LoadUserSnapshotsAsync(
            rows.SelectMany(e => new[] { e.ActorUserId, e.TargetUserId }),
            cancellationToken);
        var generatedAt = _time.GetUtcNow().UtcDateTime;
        var filterSummary = BuildFilterSummary(query);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(24);
                page.MarginVertical(20);
                page.DefaultTextStyle(TextStyle.Default.FontSize(9));

                page.Header().Column(column =>
                {
                    column.Item().Text("SecureGate").SemiBold().FontSize(14);
                    column.Item().Text("Audit Logs").FontSize(11).FontColor(Colors.Grey.Darken2);
                    column.Item().Text($"Generated (UTC): {generatedAt:u}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(filterSummary))
                        column.Item().PaddingTop(4).Text($"Filters: {filterSummary}").FontSize(8);
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(82);
                        columns.RelativeColumn(2.2f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(0.9f);
                        columns.RelativeColumn(0.9f);
                        columns.RelativeColumn(2.4f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("When (UTC)");
                        header.Cell().Element(HeaderCell).Text("Account");
                        header.Cell().Element(HeaderCell).Text("Role");
                        header.Cell().Element(HeaderCell).Text("Action");
                        header.Cell().Element(HeaderCell).Text("Module");
                        header.Cell().Element(HeaderCell).Text("Outcome");
                        header.Cell().Element(HeaderCell).Text("Severity");
                        header.Cell().Element(HeaderCell).Text("Description");
                    });

                    foreach (var row in rows)
                    {
                        var account = AuditAccountResolver.ResolveForList(row, users);
                        var accountLine = account.SecondaryLine is null
                            ? account.PrimaryName
                            : $"{account.PrimaryName}\n{account.SecondaryLine}";

                        table.Cell().Element(BodyCell).Text(row.OccurredAtUtc.ToString("u"));
                        table.Cell().Element(BodyCell).Text(accountLine);
                        table.Cell().Element(BodyCell).Text(account.RoleLabel ?? "—");
                        table.Cell().Element(BodyCell).Text(EventTypes.Label(row.EventType));
                        table.Cell().Element(BodyCell).Text(AuditModules.GetModule(row.EventType));
                        table.Cell().Element(BodyCell).Text(row.Outcome);
                        table.Cell().Element(BodyCell).Text(row.Severity);
                        table.Cell().Element(BodyCell).Text(Truncate(row.Details, 220) ?? "—");
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        await WriteExportAuditAsync(query, rows.Count, "PDF", actorId, cancellationToken);
        return document.GeneratePdf();
    }

    public Task<AuthenticationAuditLog?> FindEventAsync(long id, CancellationToken cancellationToken = default) =>
        _db.AuthenticationAuditLogs.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == id, cancellationToken);

    public async Task<AuditEventDetail?> FindEventDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var entry = await FindEventAsync(id, cancellationToken);
        if (entry is null)
            return null;

        var users = await LoadUserSnapshotsAsync([entry.ActorUserId, entry.TargetUserId], cancellationToken);
        return new AuditEventDetail
        {
            Event = entry,
            Actor = AuditAccountResolver.ResolveActorOrTarget(entry.ActorUserId, entry.SubjectIdentifier, users),
            Target = AuditAccountResolver.ResolveActorOrTarget(entry.TargetUserId, null, users),
            Module = AuditModules.GetModule(entry.EventType),
            ActionLabel = EventTypes.Label(entry.EventType)
        };
    }

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
        if (alert.Status == AlertStatuses.Resolved)
            return ServiceResult.Fail("This alert is resolved. Investigations cannot be opened or changed.");

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

    public async Task<SecurityOperationsSummary> GetSecurityOperationsSummaryAsync(CancellationToken cancellationToken = default)
    {
        return new SecurityOperationsSummary
        {
            TotalAlerts = await _db.SecurityAlerts.CountAsync(cancellationToken),
            UnresolvedAlerts = await _db.SecurityAlerts.CountAsync(a => a.Status != AlertStatuses.Resolved, cancellationToken),
            HighPriorityAlerts = await _db.SecurityAlerts.CountAsync(
                a => a.Status != AlertStatuses.Resolved &&
                     (a.Severity == Severities.High || a.Severity == Severities.Critical),
                cancellationToken),
            ActiveInvestigations = await _db.InvestigationRecords.CountAsync(
                i => i.Status != InvestigationStatuses.Closed,
                cancellationToken),
            ClosedInvestigations = await _db.InvestigationRecords.CountAsync(
                i => i.Status == InvestigationStatuses.Closed,
                cancellationToken)
        };
    }

    public async Task<AlertPage> SearchAlertsAsync(AlertQuery query, CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, _policy.MaxPageSize);
        var page = Math.Max(1, query.Page);
        var filtered = ApplyAlertFilters(_db.SecurityAlerts.AsNoTracking(), query);
        var total = await filtered.CountAsync(cancellationToken);
        var alerts = await filtered
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.AlertId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var alertIds = alerts.Select(a => a.AlertId).ToList();
        var investigations = alertIds.Count == 0
            ? new Dictionary<long, InvestigationRecord>()
            : await _db.InvestigationRecords.AsNoTracking()
                .Where(i => alertIds.Contains(i.AlertId))
                .ToDictionaryAsync(i => i.AlertId, cancellationToken);

        var items = alerts.Select(a =>
        {
            var investigation = investigations.GetValueOrDefault(a.AlertId);
            return new AlertListItem
            {
                Alert = a,
                InvestigationStatus = investigation?.Status,
                InvestigationId = investigation?.InvestigationId,
                InvestigationNotes = investigation?.Notes
            };
        }).ToList();

        return new AlertPage { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<InvestigationPage> SearchInvestigationsAsync(InvestigationQuery query, CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, _policy.MaxPageSize);
        var page = Math.Max(1, query.Page);
        var filtered = ApplyInvestigationFilters(_db.InvestigationRecords.AsNoTracking().Include(i => i.Alert), query);
        var total = await filtered.CountAsync(cancellationToken);
        var records = await filtered
            .OrderByDescending(i => i.UpdatedAtUtc)
            .ThenByDescending(i => i.InvestigationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var officerIds = records.Select(r => r.AssignedOfficerId).Distinct().ToList();
        var officers = officerIds.Count == 0
            ? new Dictionary<string, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => officerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var items = records.Select(r => new InvestigationListItem
        {
            Record = r,
            Alert = r.Alert,
            AssignedOfficerName = officers.GetValueOrDefault(r.AssignedOfficerId, "Unknown officer")
        }).ToList();

        return new InvestigationPage { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<OfficerFilterOption>> GetOfficerFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var officerRoles = new[] { Roles.SecurityOfficer, Roles.Administrator, Roles.Superadmin };
        return await (
                from user in _db.Users.AsNoTracking()
                join userRole in _db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where role.Name != null && officerRoles.Contains(role.Name)
                orderby user.DisplayName
                select new OfficerFilterOption { Id = user.Id, DisplayName = user.DisplayName })
            .Distinct()
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<SecurityAlert> ApplyAlertFilters(IQueryable<SecurityAlert> query, AlertQuery input)
    {
        if (input.FromUtc.HasValue)
            query = query.Where(a => a.CreatedAtUtc >= input.FromUtc.Value);
        if (input.ToUtcExclusive.HasValue)
            query = query.Where(a => a.CreatedAtUtc < input.ToUtcExclusive.Value);

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            if (input.Status == AlertStatuses.Resolved)
                query = query.Where(a => a.Status == AlertStatuses.Resolved);
            else if (input.Status is AlertStatuses.Open or AlertStatuses.Acknowledged)
                query = query.Where(a => a.Status == AlertStatuses.Open || a.Status == AlertStatuses.Acknowledged);
        }
        else if (!input.IncludeResolved)
            query = query.Where(a => a.Status != AlertStatuses.Resolved);

        if (!string.IsNullOrWhiteSpace(input.InvestigationStatus) &&
            InvestigationStatuses.All.Contains(input.InvestigationStatus))
        {
            query = query.Where(a =>
                _db.InvestigationRecords.Any(i => i.AlertId == a.AlertId && i.Status == input.InvestigationStatus));
        }

        if (!string.IsNullOrWhiteSpace(input.AssignedOfficerId))
        {
            var officerId = input.AssignedOfficerId.Trim();
            if (officerId.Length > 450)
                officerId = officerId[..450];
            query = query.Where(a =>
                _db.InvestigationRecords.Any(i => i.AlertId == a.AlertId && i.AssignedOfficerId == officerId));
        }

        if (!string.IsNullOrWhiteSpace(input.Severity) && Severities.AlertLevels.Contains(input.Severity))
            query = query.Where(a => a.Severity == input.Severity);

        if (!string.IsNullOrWhiteSpace(input.InvestigationLink))
        {
            query = input.InvestigationLink switch
            {
                "none" => query.Where(a => !_db.InvestigationRecords.Any(i => i.AlertId == a.AlertId)),
                InvestigationStatuses.Open => query.Where(a =>
                    _db.InvestigationRecords.Any(i => i.AlertId == a.AlertId && i.Status == InvestigationStatuses.Open)),
                InvestigationStatuses.InProgress => query.Where(a =>
                    _db.InvestigationRecords.Any(i => i.AlertId == a.AlertId && i.Status == InvestigationStatuses.InProgress)),
                InvestigationStatuses.Closed => query.Where(a =>
                    _db.InvestigationRecords.Any(i => i.AlertId == a.AlertId && i.Status == InvestigationStatuses.Closed)),
                "any" => query.Where(a => _db.InvestigationRecords.Any(i => i.AlertId == a.AlertId)),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            if (term.Length > 100)
                term = term[..100];
            query = query.Where(a =>
                a.Title.Contains(term) ||
                a.RuleKey.Contains(term) ||
                (a.SourceIp != null && a.SourceIp.Contains(term)) ||
                _db.InvestigationRecords.Any(i => i.AlertId == a.AlertId && i.Notes.Contains(term)));
        }

        return query;
    }

    private IQueryable<InvestigationRecord> ApplyInvestigationFilters(IQueryable<InvestigationRecord> query, InvestigationQuery input)
    {
        if (input.FromUtc.HasValue)
            query = query.Where(i => i.UpdatedAtUtc >= input.FromUtc.Value);
        if (input.ToUtcExclusive.HasValue)
            query = query.Where(i => i.UpdatedAtUtc < input.ToUtcExclusive.Value);

        if (!string.IsNullOrWhiteSpace(input.Status) && InvestigationStatuses.All.Contains(input.Status))
            query = query.Where(i => i.Status == input.Status);

        if (!string.IsNullOrWhiteSpace(input.AssignedOfficerId))
        {
            var officerId = input.AssignedOfficerId.Trim();
            if (officerId.Length > 450)
                officerId = officerId[..450];
            query = query.Where(i => i.AssignedOfficerId == officerId);
        }

        if (!string.IsNullOrWhiteSpace(input.AlertSeverity) && Severities.AlertLevels.Contains(input.AlertSeverity))
            query = query.Where(i => i.Alert.Severity == input.AlertSeverity);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            if (term.Length > 100)
                term = term[..100];
            query = query.Where(i =>
                i.Alert.Title.Contains(term) ||
                i.Alert.RuleKey.Contains(term) ||
                i.Notes.Contains(term));
        }

        return query;
    }

    private async Task<List<AuthenticationAuditLog>> LoadExportRowsAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        var filtered = ApplyFilters(_db.AuthenticationAuditLogs.AsNoTracking(), query);
        return await filtered
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.EventId)
            .Take(ExportRowLimit)
            .ToListAsync(cancellationToken);
    }

    private async Task WriteExportAuditAsync(AuditQuery query, int rowCount, string format, string actorId, CancellationToken cancellationToken) =>
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.ReportExported,
            Outcome = Outcomes.Success,
            Severity = Severities.Info,
            ActorUserId = actorId,
            TargetUserId = actorId,
            Details = $"Format={format}; Rows={rowCount}; From={query.FromUtc:u}; To={query.ToUtc:u}"
        }, cancellationToken);

    private static AuditListItem MapListItem(AuthenticationAuditLog e, IReadOnlyDictionary<string, AuditUserSnapshot> users) =>
        new()
        {
            Event = e,
            Account = AuditAccountResolver.ResolveForList(e, users),
            Actor = AuditAccountResolver.ResolveActorOrTarget(e.ActorUserId, e.SubjectIdentifier, users),
            Target = AuditAccountResolver.ResolveActorOrTarget(e.TargetUserId, null, users),
            Module = AuditModules.GetModule(e.EventType),
            ActionLabel = EventTypes.Label(e.EventType)
        };

    private async Task<IReadOnlyDictionary<string, AuditUserSnapshot>> LoadUserSnapshotsAsync(
        IEnumerable<string?> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<string, AuditUserSnapshot>();

        var users = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email, u.IsActive })
            .ToListAsync(cancellationToken);

        var rolePairs = await (
            from userRole in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where ids.Contains(userRole.UserId)
            select new { userRole.UserId, role.Name }).ToListAsync(cancellationToken);

        var roleByUser = rolePairs
            .GroupBy(pair => pair.UserId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.Name).FirstOrDefault());

        return users.ToDictionary(
            user => user.Id,
            user => new AuditUserSnapshot
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email,
                RoleKey = roleByUser.GetValueOrDefault(user.Id),
                IsActive = user.IsActive
            });
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

        if (!string.IsNullOrWhiteSpace(input.Module))
        {
            var types = AuditModules.EventTypesForModule(input.Module);
            if (types.Count > 0)
                query = query.Where(e => types.Contains(e.EventType));
        }

        if (!string.IsNullOrWhiteSpace(input.Role) && Roles.All.Contains(input.Role))
        {
            query = query.Where(e =>
                _db.UserRoles.Any(ur =>
                    ur.UserId == e.ActorUserId &&
                    _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == input.Role)) ||
                _db.UserRoles.Any(ur =>
                    ur.UserId == e.TargetUserId &&
                    _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == input.Role)));
        }

        if (!string.IsNullOrWhiteSpace(input.AccountUserId))
        {
            var userId = input.AccountUserId.Trim();
            if (userId.Length > 450)
                userId = userId[..450];
            query = query.Where(e => e.ActorUserId == userId || e.TargetUserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            if (term.Length > 100)
                term = term[..100];
            var moduleTypes = AuditModules.EventTypesMatchingModuleSearch(term);
            query = query.Where(e =>
                e.EventType.Contains(term) ||
                (e.Details != null && e.Details.Contains(term)) ||
                (e.SubjectIdentifier != null && e.SubjectIdentifier.Contains(term)) ||
                (moduleTypes.Count > 0 && moduleTypes.Contains(e.EventType)) ||
                _db.Users.Any(u =>
                    (u.Email == term || u.DisplayName.Contains(term) || u.Id == term) &&
                    (u.Id == e.ActorUserId || u.Id == e.TargetUserId)));
        }

        return query;
    }

    private static string BuildFilterSummary(AuditQuery query)
    {
        var parts = new List<string>
        {
            $"From {query.FromUtc:yyyy-MM-dd}",
            $"To {query.ToUtc.AddTicks(-1):yyyy-MM-dd}"
        };
        if (!string.IsNullOrWhiteSpace(query.Search))
            parts.Add($"Search={query.Search}");
        if (!string.IsNullOrWhiteSpace(query.EventType))
            parts.Add($"Action={EventTypes.Label(query.EventType)}");
        if (!string.IsNullOrWhiteSpace(query.Module))
            parts.Add($"Module={query.Module}");
        if (!string.IsNullOrWhiteSpace(query.Outcome))
            parts.Add($"Outcome={query.Outcome}");
        if (!string.IsNullOrWhiteSpace(query.Role))
            parts.Add($"Role={Roles.DisplayName(query.Role)}");
        if (!string.IsNullOrWhiteSpace(query.AccountUserId))
            parts.Add($"AccountId={query.AccountUserId}");
        return string.Join("; ", parts);
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "…";

    private static IContainer HeaderCell(IContainer container) =>
        container.DefaultTextStyle(TextStyle.Default.SemiBold().FontSize(8))
            .PaddingVertical(4)
            .PaddingHorizontal(3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2);

    private static IContainer BodyCell(IContainer container) =>
        container.PaddingVertical(3)
            .PaddingHorizontal(3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten4);
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

public sealed class SecurityOperationsSummary
{
    public int TotalAlerts { get; init; }
    public int UnresolvedAlerts { get; init; }
    public int HighPriorityAlerts { get; init; }
    public int ActiveInvestigations { get; init; }
    public int ClosedInvestigations { get; init; }
}

public sealed class AlertQuery
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Severity { get; init; }
    public string? InvestigationLink { get; init; }
    public string? InvestigationStatus { get; init; }
    public string? AssignedOfficerId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtcExclusive { get; init; }
    public bool IncludeResolved { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed class AlertListItem
{
    public SecurityAlert Alert { get; init; } = null!;
    public string? InvestigationStatus { get; init; }
    public long? InvestigationId { get; init; }
    public string? InvestigationNotes { get; init; }
}

public sealed class AlertPage
{
    public IReadOnlyList<AlertListItem> Items { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed class InvestigationQuery
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? AssignedOfficerId { get; init; }
    public string? AlertSeverity { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtcExclusive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed class InvestigationListItem
{
    public InvestigationRecord Record { get; init; } = null!;
    public SecurityAlert Alert { get; init; } = null!;
    public string AssignedOfficerName { get; init; } = "";
}

public sealed class InvestigationPage
{
    public IReadOnlyList<InvestigationListItem> Items { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed class OfficerFilterOption
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
}
