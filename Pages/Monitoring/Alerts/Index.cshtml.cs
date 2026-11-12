using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Services;

namespace SecureGate1.Pages.Monitoring.Alerts;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AlertService _alerts;
    private readonly AuditService _audit;

    public IndexModel(ApplicationDbContext db, AlertService alerts, AuditService audit)
    {
        _db = db;
        _alerts = alerts;
        _audit = audit;
    }

    public IReadOnlyList<SecurityAlert> Alerts { get; private set; } = [];
    public string? Status { get; private set; }

    public async Task OnGetAsync(string? status, CancellationToken cancellationToken)
    {
        Status = status;
        var query = _db.SecurityAlerts.AsNoTracking().AsQueryable();
        if (string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status != AlertStatuses.Resolved);
        else if (status is AlertStatuses.Open or AlertStatuses.Acknowledged or AlertStatuses.Resolved)
            query = query.Where(a => a.Status == status);
        Alerts = await query.OrderByDescending(a => a.AlertId).Take(100).ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAcknowledgeAsync(long id, CancellationToken cancellationToken)
    {
        var officerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _alerts.AcknowledgeAsync(id, officerId, cancellationToken);
        if (result.Succeeded)
        {
            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.AlertAcknowledged,
                Outcome = Outcomes.Success,
                ActorUserId = officerId,
                Details = $"Alert={id}"
            }, cancellationToken);
        }

        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToPage();
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
        return RedirectToPage();
    }
}
