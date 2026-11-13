using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Monitoring.SecurityOperations;

public class IndexModel : PageModel
{
    private readonly MonitoringService _monitoring;
    private readonly AlertService _alerts;
    private readonly AuditService _audit;
    private readonly SecurityPolicyOptions _policy;

    public IndexModel(
        MonitoringService monitoring,
        AlertService alerts,
        AuditService audit,
        IOptions<SecurityPolicyOptions> policy)
    {
        _monitoring = monitoring;
        _alerts = alerts;
        _audit = audit;
        _policy = policy.Value;
    }

    public AlertPage Results { get; private set; } = new();
    public IReadOnlyList<OfficerFilterOption> Officers { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Severity { get; set; }

    [BindProperty(SupportsGet = true, Name = "inv")]
    public string? InvestigationLink { get; set; }

    [BindProperty(SupportsGet = true, Name = "invStatus")]
    public string? InvestigationStatus { get; set; }

    [BindProperty(SupportsGet = true, Name = "assigned")]
    public string? AssignedOfficerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; private set; }
    public string? DateError { get; private set; }
    public bool HasActiveFilters { get; private set; }
    public int RangeStart => Results.Total == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
    public int RangeEnd => Results.Total == 0 ? 0 : Math.Min(Results.Total, PageNumber * PageSize);

    public async Task<IActionResult> OnGetAsync(
        string? section,
        string? alertSearch,
        string? alertStatus,
        string? alertSeverity,
        string? alertInv,
        string? alertFrom,
        string? alertTo,
        int? alertPage,
        string? invSearch,
        string? invStatus,
        string? invAssigned,
        string? invFrom,
        string? invTo,
        CancellationToken cancellationToken = default)
    {
        NormalizeLegacyQuery(alertSearch, alertStatus, alertSeverity, alertInv, alertFrom, alertTo, alertPage, invSearch, invStatus, invAssigned, invFrom, invTo);
        if (string.Equals(section, "investigations", StringComparison.OrdinalIgnoreCase))
        {
            var legacy = new
            {
                Search = Search ?? invSearch,
                InvestigationStatus = InvestigationStatus ?? invStatus,
                AssignedOfficerId = AssignedOfficerId ?? invAssigned,
                From = From ?? invFrom,
                To = To ?? invTo
            };
            Search = legacy.Search;
            InvestigationStatus = legacy.InvestigationStatus;
            AssignedOfficerId = legacy.AssignedOfficerId;
            From = legacy.From;
            To = legacy.To;
        }

        PageSize = _policy.PageSize;
        Officers = await _monitoring.GetOfficerFilterOptionsAsync(cancellationToken);
        HasActiveFilters = HasFilterValues();
        PageNumber = Math.Max(1, PageNumber);

        var (fromUtc, toExclusive, rangeError) = ParseDateRange(From, To);
        DateError = rangeError;

        if (DateError is not null)
            return Page();

        var query = new AlertQuery
        {
            Search = Search,
            Status = Status,
            Severity = Severity,
            InvestigationLink = InvestigationLink,
            InvestigationStatus = InvestigationStatus,
            AssignedOfficerId = AssignedOfficerId,
            FromUtc = fromUtc,
            ToUtcExclusive = toExclusive,
            IncludeResolved = HasActiveFilters,
            Page = PageNumber,
            PageSize = PageSize
        };
        Results = await _monitoring.SearchAlertsAsync(query, cancellationToken);
        if (PageNumber > Results.TotalPages && Results.TotalPages > 0)
        {
            PageNumber = Results.TotalPages;
            query = new AlertQuery
            {
                Search = query.Search,
                Status = query.Status,
                Severity = query.Severity,
                InvestigationLink = query.InvestigationLink,
                InvestigationStatus = query.InvestigationStatus,
                AssignedOfficerId = query.AssignedOfficerId,
                FromUtc = query.FromUtc,
                ToUtcExclusive = query.ToUtcExclusive,
                IncludeResolved = query.IncludeResolved,
                Page = PageNumber,
                PageSize = query.PageSize
            };
            Results = await _monitoring.SearchAlertsAsync(query, cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostResolveAsync(long id, CancellationToken cancellationToken)
    {
        var officerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _alerts.ResolveAsync(id, officerId, cancellationToken);
        if (result.Succeeded)
        {
            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.AlertResolved,
                Outcome = Outcomes.Success,
                ActorUserId = officerId,
                Details = $"Alert={id}"
            }, cancellationToken);
        }

        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToPage(RouteValues());
    }

    public async Task<IActionResult> OnPostSaveInvestigationAsync(
        long alertId,
        string investigateStatus,
        string investigateNotes,
        CancellationToken cancellationToken)
    {
        var officerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _monitoring.SaveInvestigationAsync(
            alertId,
            officerId,
            investigateStatus,
            investigateNotes,
            cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToPage(RouteValues());
    }

    public object RouteValues() => new
    {
        search = Search,
        status = Status,
        severity = Severity,
        inv = InvestigationLink,
        invStatus = InvestigationStatus,
        assigned = AssignedOfficerId,
        from = From,
        to = To,
        pageNumber = PageNumber
    };

    private void NormalizeLegacyQuery(
        string? alertSearch,
        string? alertStatus,
        string? alertSeverity,
        string? alertInv,
        string? alertFrom,
        string? alertTo,
        int? alertPage,
        string? invSearch,
        string? invStatus,
        string? invAssigned,
        string? invFrom,
        string? invTo)
    {
        if (string.IsNullOrWhiteSpace(Search) && !string.IsNullOrWhiteSpace(alertSearch))
            Search = alertSearch;
        if (string.IsNullOrWhiteSpace(Search) && !string.IsNullOrWhiteSpace(invSearch))
            Search = invSearch;
        if (string.IsNullOrWhiteSpace(Status) && !string.IsNullOrWhiteSpace(alertStatus))
            Status = alertStatus == AlertStatuses.Acknowledged ? AlertStatuses.Open : alertStatus;
        if (string.IsNullOrWhiteSpace(Severity) && !string.IsNullOrWhiteSpace(alertSeverity))
            Severity = alertSeverity;
        if (string.IsNullOrWhiteSpace(InvestigationLink) && !string.IsNullOrWhiteSpace(alertInv))
            InvestigationLink = alertInv;
        if (string.IsNullOrWhiteSpace(From) && !string.IsNullOrWhiteSpace(alertFrom))
            From = alertFrom;
        if (string.IsNullOrWhiteSpace(From) && !string.IsNullOrWhiteSpace(invFrom))
            From = invFrom;
        if (string.IsNullOrWhiteSpace(To) && !string.IsNullOrWhiteSpace(alertTo))
            To = alertTo;
        if (string.IsNullOrWhiteSpace(To) && !string.IsNullOrWhiteSpace(invTo))
            To = invTo;
        if (string.IsNullOrWhiteSpace(InvestigationStatus) && !string.IsNullOrWhiteSpace(invStatus))
            InvestigationStatus = invStatus;
        if (string.IsNullOrWhiteSpace(AssignedOfficerId) && !string.IsNullOrWhiteSpace(invAssigned))
            AssignedOfficerId = invAssigned;
        if (PageNumber == 1 && alertPage is > 1)
            PageNumber = alertPage.Value;
        if (Status == AlertStatuses.Acknowledged)
            Status = AlertStatuses.Open;
    }

    private bool HasFilterValues() =>
        !string.IsNullOrWhiteSpace(Search) ||
        !string.IsNullOrWhiteSpace(Status) ||
        !string.IsNullOrWhiteSpace(Severity) ||
        !string.IsNullOrWhiteSpace(InvestigationLink) ||
        !string.IsNullOrWhiteSpace(InvestigationStatus) ||
        !string.IsNullOrWhiteSpace(AssignedOfficerId) ||
        !string.IsNullOrWhiteSpace(From) ||
        !string.IsNullOrWhiteSpace(To) ||
        PageNumber > 1;

    private (DateTime? FromUtc, DateTime? ToExclusive, string? Error) ParseDateRange(string? from, string? to)
    {
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
            return (null, null, null);

        if (!DateOnly.TryParse(from, out var fromDay) || !DateOnly.TryParse(to, out var toDay))
            return (null, null, "Use valid dates in the filter.");

        var fromUtc = fromDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toExclusive = toDay.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeError = _monitoring.ValidateRange(fromUtc, toExclusive);
        return (fromUtc, toExclusive, rangeError);
    }

    public static string InvestigationStatusLabel(string status) => status switch
    {
        InvestigationStatuses.InProgress => "In progress",
        InvestigationStatuses.Closed => "Closed",
        _ => "Open"
    };

    public static string AlertStatusLabel(string status) =>
        status == AlertStatuses.Acknowledged ? AlertStatuses.Open : status;

    public static string SeverityBadgeClass(string severity) => severity switch
    {
        Severities.Critical or Severities.High => "sg-badge-bad",
        Severities.Medium => "sg-badge-warn",
        Severities.Low => "sg-badge-neutral",
        _ => "sg-badge"
    };

    public static string AlertStatusBadgeClass(string status) =>
        AlertStatusLabel(status) == AlertStatuses.Resolved ? "sg-badge-good" : "sg-badge-bad";

    public static string InvestigationStatusBadgeClass(string status) => status switch
    {
        InvestigationStatuses.Closed => "sg-badge-good",
        InvestigationStatuses.InProgress => "sg-badge-warn",
        _ => "sg-badge-bad"
    };
}
