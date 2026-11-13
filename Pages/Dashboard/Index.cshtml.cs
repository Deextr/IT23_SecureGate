using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Services;

namespace SecureGate1.Pages.Dashboard;

public class IndexModel : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly DashboardService _dashboard;

    public IndexModel(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public DashboardPayload Payload { get; private set; } = new();
    public DashboardKind Kind { get; private set; }
    public string PayloadJson { get; private set; } = "{}";

    public async Task OnGetAsync(string? preset, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        Kind = _dashboard.KindFor(User);
        Payload = await _dashboard.GetAsync(User, preset, from, to, cancellationToken);
        PayloadJson = JsonSerializer.Serialize(Payload, JsonOptions);
        ViewData["Title"] = "Dashboard";
    }

    public async Task<IActionResult> OnGetRefreshAsync(string? preset, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await _dashboard.GetAsync(User, preset, from, to, cancellationToken);
            return new JsonResult(payload, JsonOptions);
        }
        catch (Exception)
        {
            return new JsonResult(new DashboardPayload { Error = "Dashboard data could not be loaded." }, JsonOptions)
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
