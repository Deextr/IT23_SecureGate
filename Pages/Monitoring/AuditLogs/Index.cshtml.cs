using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Monitoring.AuditLogs;

public class IndexModel : PageModel
{
    private readonly MonitoringService _monitoring;

    public IndexModel(MonitoringService monitoring)
    {
        _monitoring = monitoring;
    }

    public AuditPage Results { get; private set; } = new();
    public string From { get; private set; } = "";
    public string To { get; private set; } = "";
    public string? EventType { get; private set; }
    public string? Outcome { get; private set; }
    public string? Severity { get; private set; }
    public string? Account { get; private set; }
    public int PageNumber { get; private set; } = 1;
    public int PageSize { get; private set; } = 25;
    public string? Error { get; private set; }

    public async Task OnGetAsync(DateOnly? from, DateOnly? to, string? eventType, string? outcome, string? severity, string? account, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? today.AddDays(-7);
        var end = to ?? today;
        From = start.ToString("yyyy-MM-dd");
        To = end.ToString("yyyy-MM-dd");
        EventType = eventType;
        Outcome = outcome;
        Severity = severity;
        Account = account;
        PageNumber = pageNumber;
        PageSize = pageSize;
        var query = Build(start, end, eventType, outcome, severity, account, pageNumber, pageSize);
        var rangeError = _monitoring.ValidateRange(query.FromUtc, query.ToUtc);
        if (rangeError is not null)
        {
            Error = rangeError;
            Results = new AuditPage();
            return;
        }

        Results = await _monitoring.SearchAsync(query, cancellationToken);
    }

    public async Task<IActionResult> OnGetExportAsync(DateOnly? from, DateOnly? to, string? eventType, string? outcome, string? severity, string? account, CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.Superadmin) && !User.IsInRole(Roles.Administrator))
            return Forbid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = Build(from ?? today.AddDays(-7), to ?? today, eventType, outcome, severity, account, 1, 25);
        var rangeError = _monitoring.ValidateRange(query.FromUtc, query.ToUtc);
        if (rangeError is not null)
            return BadRequest(rangeError);

        var bytes = await _monitoring.ExportCsvAsync(query, User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
        return File(bytes, "text/csv", "securegate-audit.csv");
    }

    private static AuditQuery Build(DateOnly from, DateOnly to, string? eventType, string? outcome, string? severity, string? account, int pageNumber, int pageSize)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
        return new AuditQuery
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            EventType = eventType,
            Outcome = outcome,
            Severity = severity,
            Account = account,
            Page = pageNumber,
            PageSize = pageSize
        };
    }
}
