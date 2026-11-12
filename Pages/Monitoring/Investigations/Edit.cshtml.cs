using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Services;

namespace SecureGate1.Pages.Monitoring.Investigations;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly MonitoringService _monitoring;

    public EditModel(ApplicationDbContext db, MonitoringService monitoring)
    {
        _db = db;
        _monitoring = monitoring;
    }

    public SecurityAlert? Alert { get; private set; }

    [BindProperty]
    public string Status { get; set; } = InvestigationStatuses.Open;

    [BindProperty]
    public string Notes { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(long alertId, CancellationToken cancellationToken)
    {
        await LoadAsync(alertId, cancellationToken);
        return Alert is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync(long alertId, CancellationToken cancellationToken)
    {
        var result = await _monitoring.SaveInvestigationAsync(
            alertId,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            Status,
            Notes,
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await LoadAsync(alertId, cancellationToken);
            return Alert is null ? NotFound() : Page();
        }

        TempData["Success"] = result.Message;
        return RedirectToPage(new { alertId });
    }

    private async Task LoadAsync(long alertId, CancellationToken cancellationToken)
    {
        Alert = await _db.SecurityAlerts.AsNoTracking().SingleOrDefaultAsync(a => a.AlertId == alertId, cancellationToken);
        var record = await _db.InvestigationRecords.AsNoTracking().SingleOrDefaultAsync(i => i.AlertId == alertId, cancellationToken);
        if (record is not null && !Request.HasFormContentType)
        {
            Status = record.Status;
            Notes = record.Notes;
        }
    }
}
