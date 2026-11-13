using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Services;

namespace SecureGate1.Pages.Monitoring.AuditLogs;

public class DetailsModel : PageModel
{
    private readonly MonitoringService _monitoring;

    public DetailsModel(MonitoringService monitoring)
    {
        _monitoring = monitoring;
    }

    public AuditEventDetail? Detail { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        Detail = await _monitoring.FindEventDetailAsync(id, cancellationToken);
        return Detail is null ? NotFound() : Page();
    }
}
