using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Models;
using SecureGate1.Services;

namespace SecureGate1.Pages.Monitoring.AuditLogs;

public class DetailsModel : PageModel
{
    private readonly MonitoringService _monitoring;

    public DetailsModel(MonitoringService monitoring)
    {
        _monitoring = monitoring;
    }

    public AuthenticationAuditLog? Event { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        Event = await _monitoring.FindEventAsync(id, cancellationToken);
        return Event is null ? NotFound() : Page();
    }
}
