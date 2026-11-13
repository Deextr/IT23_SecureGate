using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Monitoring.AuditLogs;

public class IndexModel : PageModel
{
    private readonly MonitoringService _monitoring;
    private readonly SecurityPolicyOptions _policy;

    public IndexModel(MonitoringService monitoring, IOptions<SecurityPolicyOptions> policy)
    {
        _monitoring = monitoring;
        _policy = policy.Value;
    }

    public AuditPage Results { get; private set; } = new();
    public string From { get; private set; } = "";
    public string To { get; private set; } = "";
    public string? Search { get; private set; }
    public string? EventType { get; private set; }
    public string? Outcome { get; private set; }
    public string? Role { get; private set; }
    public string? Module { get; private set; }
    public string? AccountUserId { get; private set; }
    public int PageNumber { get; private set; } = 1;
    public int PageSize { get; private set; }
    public string? Error { get; private set; }
    public bool CanExport { get; private set; }
    public bool HasActiveFilters { get; private set; }
    public int RangeStart => Results.Total == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
    public int RangeEnd => Results.Total == 0 ? 0 : Math.Min(Results.Total, PageNumber * PageSize);

    public async Task OnGetAsync(
        DateOnly? from,
        DateOnly? to,
        string? search,
        string? eventType,
        string? outcome,
        string? role,
        string? module,
        string? accountUserId,
        string? account,
        int pageNumber = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        CanExport = User.IsInRole(Roles.Superadmin) || User.IsInRole(Roles.Administrator);
        var defaultPageSize = _policy.PageSize;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? today.AddDays(-7);
        var end = to ?? today;
        From = start.ToString("yyyy-MM-dd");
        To = end.ToString("yyyy-MM-dd");
        Search = search;
        EventType = eventType;
        Outcome = outcome;
        Role = role;
        Module = module;
        AccountUserId = accountUserId;
        if (string.IsNullOrWhiteSpace(AccountUserId) && !string.IsNullOrWhiteSpace(account))
            AccountUserId = account;
        PageNumber = pageNumber;
        PageSize = pageSize is null or < 1 ? defaultPageSize : Math.Clamp(pageSize.Value, 1, _policy.MaxPageSize);

        HasActiveFilters =
            !string.IsNullOrWhiteSpace(Search) ||
            !string.IsNullOrWhiteSpace(EventType) ||
            !string.IsNullOrWhiteSpace(Outcome) ||
            !string.IsNullOrWhiteSpace(Role) ||
            !string.IsNullOrWhiteSpace(Module) ||
            !string.IsNullOrWhiteSpace(AccountUserId) ||
            from is not null ||
            to is not null ||
            pageNumber > 1 ||
            (pageSize is not null && pageSize != defaultPageSize);

        var query = Build(start, end, Search, EventType, Outcome, Role, Module, AccountUserId, PageNumber, PageSize);
        var rangeError = _monitoring.ValidateRange(query.FromUtc, query.ToUtc);
        if (rangeError is not null)
        {
            Error = rangeError;
            Results = new AuditPage();
            return;
        }

        Results = await _monitoring.SearchAsync(query, cancellationToken);
        if (PageNumber > Results.TotalPages && Results.TotalPages > 0)
        {
            PageNumber = Results.TotalPages;
            query = Build(start, end, Search, EventType, Outcome, Role, Module, AccountUserId, PageNumber, PageSize);
            Results = await _monitoring.SearchAsync(query, cancellationToken);
        }
    }

    public async Task<IActionResult> OnGetExportCsvAsync(
        DateOnly? from,
        DateOnly? to,
        string? search,
        string? eventType,
        string? outcome,
        string? role,
        string? module,
        string? accountUserId,
        string? account,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.Superadmin) && !User.IsInRole(Roles.Administrator))
            return Forbid();

        return await ExportAsync(from, to, search, eventType, outcome, role, module, accountUserId ?? account, "csv", cancellationToken);
    }

    public async Task<IActionResult> OnGetExportPdfAsync(
        DateOnly? from,
        DateOnly? to,
        string? search,
        string? eventType,
        string? outcome,
        string? role,
        string? module,
        string? accountUserId,
        string? account,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.Superadmin) && !User.IsInRole(Roles.Administrator))
            return Forbid();

        return await ExportAsync(from, to, search, eventType, outcome, role, module, accountUserId ?? account, "pdf", cancellationToken);
    }

    private async Task<IActionResult> ExportAsync(
        DateOnly? from,
        DateOnly? to,
        string? search,
        string? eventType,
        string? outcome,
        string? role,
        string? module,
        string? accountUserId,
        string format,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = Build(from ?? today.AddDays(-7), to ?? today, search, eventType, outcome, role, module, accountUserId, 1, _policy.PageSize);
        var rangeError = _monitoring.ValidateRange(query.FromUtc, query.ToUtc);
        if (rangeError is not null)
            return BadRequest(rangeError);

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (format == "pdf")
        {
            var pdf = await _monitoring.ExportPdfAsync(query, actorId, cancellationToken);
            return File(pdf, "application/pdf", "securegate-audit-logs.pdf");
        }

        var csv = await _monitoring.ExportCsvAsync(query, actorId, cancellationToken);
        return File(csv, "text/csv", "securegate-audit-logs.csv");
    }

    private static AuditQuery Build(
        DateOnly from,
        DateOnly to,
        string? search,
        string? eventType,
        string? outcome,
        string? role,
        string? module,
        string? accountUserId,
        int pageNumber,
        int pageSize)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
        return new AuditQuery
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Search = search,
            EventType = eventType,
            Outcome = outcome,
            Role = role,
            Module = module,
            AccountUserId = accountUserId,
            Page = pageNumber,
            PageSize = pageSize
        };
    }
}
