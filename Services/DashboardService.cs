using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;

namespace SecureGate1.Services;

public sealed class DashboardService
{
    private static readonly string[] SecurityActivityTypes =
    [
        EventTypes.PasswordFailed,
        EventTypes.AccountLocked,
        EventTypes.SignInRejectedLocked,
        EventTypes.SignInRejectedDisabled,
        EventTypes.TotpFailed,
        EventTypes.TotpCooldown,
        EventTypes.RecoveryCodeFailed,
        EventTypes.RecoveryCooldown,
        EventTypes.AccessDenied,
        EventTypes.IpRateLimited,
        EventTypes.SessionRejected,
        EventTypes.AlertAcknowledged,
        EventTypes.AlertResolved,
        EventTypes.InvestigationUpdated
    ];

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SecurityPolicyOptions _policy;
    private readonly TimeProvider _time;

    public DashboardService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        IOptions<SecurityPolicyOptions> policy,
        TimeProvider time)
    {
        _db = db;
        _users = users;
        _policy = policy.Value;
        _time = time;
    }

    public DashboardKind KindFor(ClaimsPrincipal user)
    {
        if (user.IsInRole(Roles.Superadmin) || user.IsInRole(Roles.Administrator))
            return DashboardKind.Administrator;
        if (user.IsInRole(Roles.SecurityOfficer))
            return DashboardKind.SecurityOfficer;
        return DashboardKind.Employee;
    }

    public async Task<DashboardPayload> GetAsync(
        ClaimsPrincipal principal,
        string? preset,
        DateOnly? fromLocal,
        DateOnly? toLocal,
        CancellationToken cancellationToken = default)
    {
        var kind = KindFor(principal);
        var range = DashboardDateRange.Resolve(
            preset,
            fromLocal,
            toLocal,
            _time,
            TimeZoneInfo.Local,
            _policy.MaxAuditRangeDays,
            out var error);

        if (range is null)
        {
            return new DashboardPayload
            {
                Kind = kind.ToString(),
                Preset = string.IsNullOrWhiteSpace(preset) ? DashboardPresets.Last7Days : preset.Trim(),
                From = fromLocal?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                To = toLocal?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                RangeLabel = error ?? "Invalid date range",
                Error = error
            };
        }

        var payload = new DashboardPayload
        {
            Kind = kind.ToString(),
            Preset = range.Preset,
            From = range.FromLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            To = range.ToLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            RangeLabel = range.Label
        };

        return kind switch
        {
            DashboardKind.Administrator => payload with { Admin = await LoadAdminAsync(range, cancellationToken) },
            DashboardKind.SecurityOfficer => payload with { Officer = await LoadOfficerAsync(range, cancellationToken) },
            _ => payload with { Employee = await LoadEmployeeAsync(principal, range, cancellationToken) }
        };
    }

    private async Task<AdminDashboardDto> LoadAdminAsync(DashboardRange range, CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        var roleCounts = await RoleCountsAsync(cancellationToken);
        var administrators = CountRole(roleCounts, Roles.Superadmin) + CountRole(roleCounts, Roles.Administrator);

        var recentAudit = await _db.AuthenticationAuditLogs.AsNoTracking()
            .Where(e => e.OccurredAtUtc >= from && e.OccurredAtUtc < to)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.EventId)
            .Take(8)
            .ToListAsync(cancellationToken);

        var recentAlerts = await _db.SecurityAlerts.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= from && a.CreatedAtUtc < to)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.AlertId)
            .Take(6)
            .ToListAsync(cancellationToken);

        var pending = await _db.MfaResetRequests.AsNoTracking()
            .Where(r => r.Status == ResetRequestStatuses.Pending)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(6)
            .Select(r => new { r.MfaResetRequestId, r.CreatedAtUtc, Target = r.TargetUser.DisplayName })
            .ToListAsync(cancellationToken);

        var activity = await SeriesAsync(
            _db.AuthenticationAuditLogs.AsNoTracking()
                .Where(e => e.OccurredAtUtc >= from && e.OccurredAtUtc < to)
                .Select(e => e.OccurredAtUtc),
            range,
            cancellationToken);

        var created = await SeriesAsync(
            _db.Users.AsNoTracking()
                .Where(u => u.CreatedAtUtc >= from && u.CreatedAtUtc < to)
                .Select(u => u.CreatedAtUtc),
            range,
            cancellationToken);

        var alertSeverity = await _db.SecurityAlerts.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= from && a.CreatedAtUtc < to)
            .GroupBy(a => a.Severity)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var investigationStatus = await _db.InvestigationRecords.AsNoTracking()
            .Where(i => i.CreatedAtUtc >= from && i.CreatedAtUtc < to)
            .GroupBy(i => i.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new AdminDashboardDto
        {
            TotalAccounts = await _db.Users.CountAsync(cancellationToken),
            ActiveAccounts = await _db.Users.CountAsync(u => u.IsActive, cancellationToken),
            DisabledAccounts = await _db.Users.CountAsync(u => !u.IsActive, cancellationToken),
            Administrators = administrators,
            SecurityOfficers = CountRole(roleCounts, Roles.SecurityOfficer),
            Employees = CountRole(roleCounts, Roles.Employee),
            PendingRecovery = await _db.MfaResetRequests.CountAsync(r => r.Status == ResetRequestStatuses.Pending, cancellationToken),
            AlertsInRange = await _db.SecurityAlerts.CountAsync(a => a.CreatedAtUtc >= from && a.CreatedAtUtc < to, cancellationToken),
            OpenAlerts = await _db.SecurityAlerts.CountAsync(a => a.Status != AlertStatuses.Resolved, cancellationToken),
            OngoingInvestigations = await _db.InvestigationRecords.CountAsync(
                i => i.Status != InvestigationStatuses.Closed, cancellationToken),
            AuditEventsInRange = await _db.AuthenticationAuditLogs.CountAsync(
                e => e.OccurredAtUtc >= from && e.OccurredAtUtc < to, cancellationToken),
            AccountsCreatedInRange = await _db.Users.CountAsync(
                u => u.CreatedAtUtc >= from && u.CreatedAtUtc < to, cancellationToken),
            ActivityTrend = activity,
            AccountOverview = new DashboardChartSeries
            {
                Labels = ["Administrators", "Security officers", "Employees"],
                Values = [administrators, CountRole(roleCounts, Roles.SecurityOfficer), CountRole(roleCounts, Roles.Employee)]
            },
            AlertSeverity = NamedSeries(
                new[] { Severities.Critical, Severities.High, Severities.Medium, Severities.Low },
                alertSeverity.ToDictionary(x => x.Key, x => x.Count)),
            InvestigationStatus = NamedSeries(
                new[] { InvestigationStatuses.Open, InvestigationStatuses.InProgress, InvestigationStatuses.Closed },
                investigationStatus.ToDictionary(x => x.Key, x => x.Count),
                key => key switch
                {
                    InvestigationStatuses.Open => "Pending",
                    InvestigationStatuses.InProgress => "Ongoing",
                    InvestigationStatuses.Closed => "Resolved",
                    _ => key
                }),
            AccountCreationTrend = created,
            RecentAudit = recentAudit.Select(e => new DashboardEventItem
            {
                When = DisplayTime.Local(e.OccurredAtUtc),
                Title = EventTypes.Label(e.EventType),
                Detail = e.Outcome,
                Href = "/Monitoring/AuditLogs/Details?id=" + e.EventId
            }).ToList(),
            RecentAlerts = recentAlerts.Select(MapAlert).ToList(),
            PendingActions = pending.Select(r => new DashboardRequestItem
            {
                Title = "Recovery for " + r.Target,
                Status = ResetRequestStatuses.Pending,
                When = DisplayTime.Local(r.CreatedAtUtc),
                Href = "/Admin/MfaResets/Index?request=" + r.MfaResetRequestId
            }).ToList()
        };
    }

    private async Task<OfficerDashboardDto> LoadOfficerAsync(DashboardRange range, CancellationToken cancellationToken)
    {
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;

        var recentAlerts = await _db.SecurityAlerts.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= from && a.CreatedAtUtc < to)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.AlertId)
            .Take(8)
            .ToListAsync(cancellationToken);

        var securityTypes = SecurityActivityTypes;
        var recentEvents = await _db.AuthenticationAuditLogs.AsNoTracking()
            .Where(e => e.OccurredAtUtc >= from && e.OccurredAtUtc < to && securityTypes.Contains(e.EventType))
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.EventId)
            .Take(8)
            .ToListAsync(cancellationToken);

        var alertSeverity = await _db.SecurityAlerts.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= from && a.CreatedAtUtc < to)
            .GroupBy(a => a.Severity)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var investigationStatus = await _db.InvestigationRecords.AsNoTracking()
            .Where(i => i.CreatedAtUtc >= from && i.CreatedAtUtc < to)
            .GroupBy(i => i.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var alertTrend = await SeriesAsync(
            _db.SecurityAlerts.AsNoTracking()
                .Where(a => a.CreatedAtUtc >= from && a.CreatedAtUtc < to)
                .Select(a => a.CreatedAtUtc),
            range,
            cancellationToken);

        return new OfficerDashboardDto
        {
            AlertsInRange = await _db.SecurityAlerts.CountAsync(
                a => a.CreatedAtUtc >= from && a.CreatedAtUtc < to, cancellationToken),
            UnacknowledgedAlerts = await _db.SecurityAlerts.CountAsync(
                a => a.Status == AlertStatuses.Open, cancellationToken),
            OpenAlerts = await _db.SecurityAlerts.CountAsync(
                a => a.Status != AlertStatuses.Resolved, cancellationToken),
            HighPriorityOpen = await _db.SecurityAlerts.CountAsync(
                a => a.Status != AlertStatuses.Resolved &&
                     (a.Severity == Severities.High || a.Severity == Severities.Critical),
                cancellationToken),
            PendingInvestigations = await _db.InvestigationRecords.CountAsync(
                i => i.Status == InvestigationStatuses.Open, cancellationToken),
            OngoingInvestigations = await _db.InvestigationRecords.CountAsync(
                i => i.Status == InvestigationStatuses.InProgress, cancellationToken),
            ResolvedInvestigationsInRange = await _db.InvestigationRecords.CountAsync(
                i => i.Status == InvestigationStatuses.Closed &&
                     i.ClosedAtUtc != null &&
                     i.ClosedAtUtc >= from &&
                     i.ClosedAtUtc < to,
                cancellationToken),
            AlertSeverity = NamedSeries(
                new[] { Severities.Critical, Severities.High, Severities.Medium, Severities.Low },
                alertSeverity.ToDictionary(x => x.Key, x => x.Count)),
            InvestigationStatus = NamedSeries(
                new[] { InvestigationStatuses.Open, InvestigationStatuses.InProgress, InvestigationStatuses.Closed },
                investigationStatus.ToDictionary(x => x.Key, x => x.Count),
                key => key switch
                {
                    InvestigationStatuses.Open => "Pending",
                    InvestigationStatuses.InProgress => "Ongoing",
                    InvestigationStatuses.Closed => "Resolved",
                    _ => key
                }),
            AlertTrend = alertTrend,
            RecentAlerts = recentAlerts.Select(MapAlert).ToList(),
            RecentSecurityActivity = recentEvents.Select(e => new DashboardEventItem
            {
                When = DisplayTime.Local(e.OccurredAtUtc),
                Title = EventTypes.Label(e.EventType),
                Detail = e.Outcome,
                Href = "/Monitoring/AuditLogs/Details?id=" + e.EventId
            }).ToList()
        };
    }

    private async Task<EmployeeDashboardDto> LoadEmployeeAsync(
        ClaimsPrincipal principal,
        DashboardRange range,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetUserAsync(principal);
        if (user is null)
            return new EmployeeDashboardDto { AccountStatus = "Unknown" };

        var userId = user.Id;
        var from = range.FromUtc;
        var to = range.ToUtcExclusive;
        var roles = await _users.GetRolesAsync(user);
        var enrolled = await _db.UserMfaSettings.AsNoTracking()
            .AnyAsync(s => s.UserId == userId && s.IsEnabled, cancellationToken);

        var events = _db.AuthenticationAuditLogs.AsNoTracking()
            .Where(e => e.OccurredAtUtc >= from && e.OccurredAtUtc < to &&
                        (e.ActorUserId == userId || e.TargetUserId == userId));

        var recent = await events
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.EventId)
            .Take(8)
            .ToListAsync(cancellationToken);

        var notifications = await _db.SecurityAlerts.AsNoTracking()
            .Where(a => a.TargetUserId == userId && a.CreatedAtUtc >= from && a.CreatedAtUtc < to)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(6)
            .ToListAsync(cancellationToken);

        var requests = await _db.MfaResetRequests.AsNoTracking()
            .Where(r => r.TargetUserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(6)
            .ToListAsync(cancellationToken);

        var activity = await SeriesAsync(events.Select(e => e.OccurredAtUtc), range, cancellationToken);

        return new EmployeeDashboardDto
        {
            DisplayName = user.DisplayName,
            Email = user.Email ?? "",
            RoleName = string.Join(", ", roles.Select(Roles.DisplayName)),
            AccountStatus = user.IsActive ? "Active" : "Disabled",
            LastSignIn = DisplayTime.Local(user.LastSignInAtUtc),
            Authenticator = enrolled ? "Enrolled" : "Not enrolled",
            PersonalEventsInRange = await events.CountAsync(cancellationToken),
            OpenPersonalAlerts = await _db.SecurityAlerts.CountAsync(
                a => a.TargetUserId == userId && a.Status != AlertStatuses.Resolved, cancellationToken),
            PendingRequests = await _db.MfaResetRequests.CountAsync(
                r => r.TargetUserId == userId && r.Status == ResetRequestStatuses.Pending, cancellationToken),
            PersonalActivityTrend = activity,
            RecentActivity = recent.Select(e => new DashboardEventItem
            {
                When = DisplayTime.Local(e.OccurredAtUtc),
                Title = EventTypes.Label(e.EventType),
                Detail = e.Outcome
            }).ToList(),
            PersonalNotifications = notifications.Select(a => new DashboardAlertItem
            {
                Title = a.Title,
                Severity = a.Severity,
                Status = a.Status,
                When = DisplayTime.Local(a.CreatedAtUtc)
            }).ToList(),
            Requests = requests.Select(r => new DashboardRequestItem
            {
                Title = "Authenticator recovery",
                Status = r.Status,
                When = DisplayTime.Local(r.CreatedAtUtc)
            }).ToList()
        };
    }

    private async Task<Dictionary<string, int>> RoleCountsAsync(CancellationToken cancellationToken)
    {
        var pairs = await (
            from userRole in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            group userRole by role.Name into g
            select new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        return pairs
            .Where(p => !string.IsNullOrEmpty(p.Key))
            .ToDictionary(p => p.Key!, p => p.Count, StringComparer.Ordinal);
    }

    private static int CountRole(IReadOnlyDictionary<string, int> counts, string role) =>
        counts.TryGetValue(role, out var value) ? value : 0;

    private static DashboardAlertItem MapAlert(SecurityAlert alert) =>
        new()
        {
            Title = alert.Title,
            Severity = alert.Severity,
            Status = alert.Status,
            When = DisplayTime.Local(alert.CreatedAtUtc),
            Href = "/Monitoring/SecurityOperations/Index"
        };

    private static DashboardChartSeries NamedSeries(
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, int> counts,
        Func<string, string>? label = null) =>
        new()
        {
            Labels = keys.Select(k => label?.Invoke(k) ?? k).ToList(),
            Values = keys.Select(k => counts.TryGetValue(k, out var n) ? n : 0).ToList()
        };

    private async Task<DashboardChartSeries> SeriesAsync(
        IQueryable<DateTime> source,
        DashboardRange range,
        CancellationToken cancellationToken)
    {
        var buckets = BuildBuckets(range);
        if (buckets.Count == 0)
            return new DashboardChartSeries();

        var rows = await source
            .GroupBy(d => new { d.Year, d.Month, d.Day, d.Hour })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var counts = new int[buckets.Count];
        foreach (var row in rows)
        {
            var stamp = new DateTime(row.Year, row.Month, row.Day, row.Hour, 0, 0, DateTimeKind.Utc);
            if (stamp < range.FromUtc || stamp >= range.ToUtcExclusive)
                continue;
            for (var i = buckets.Count - 1; i >= 0; i--)
            {
                if (stamp >= buckets[i].StartUtc)
                {
                    counts[i] += row.Count;
                    break;
                }
            }
        }

        return new DashboardChartSeries
        {
            Labels = buckets.Select(b => b.Label).ToList(),
            Values = counts
        };
    }

    private static List<(DateTime StartUtc, string Label)> BuildBuckets(DashboardRange range)
    {
        var days = Math.Max(1, (int)Math.Ceiling((range.ToUtcExclusive - range.FromUtc).TotalDays));
        var buckets = new List<(DateTime, string)>();
        if (days <= 2)
        {
            for (var t = range.FromUtc; t < range.ToUtcExclusive; t = t.AddHours(1))
            {
                var local = t.ToLocalTime();
                buckets.Add((t, local.ToString("h tt", CultureInfo.InvariantCulture)));
            }

            return buckets;
        }

        if (days <= 62)
        {
            for (var date = range.FromLocal; date <= range.ToLocal; date = date.AddDays(1))
            {
                buckets.Add((DashboardDateRange.ToUtc(date, TimeZoneInfo.Local), date.ToString("MMM d", CultureInfo.InvariantCulture)));
            }

            return buckets;
        }

        var cursor = new DateOnly(range.FromLocal.Year, range.FromLocal.Month, 1);
        var last = new DateOnly(range.ToLocal.Year, range.ToLocal.Month, 1);
        while (cursor <= last)
        {
            var start = cursor < range.FromLocal ? range.FromLocal : cursor;
            buckets.Add((DashboardDateRange.ToUtc(start, TimeZoneInfo.Local), cursor.ToString("MMM yyyy", CultureInfo.InvariantCulture)));
            cursor = cursor.AddMonths(1);
        }

        return buckets;
    }
}
