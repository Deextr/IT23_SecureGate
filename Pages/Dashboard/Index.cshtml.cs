using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Dashboard;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly MonitoringService _monitoring;

    public IndexModel(UserManager<ApplicationUser> users, MonitoringService monitoring)
    {
        _users = users;
        _monitoring = monitoring;
    }

    public string DisplayName { get; private set; } = "";
    public string RoleName { get; private set; } = "";
    public DashboardSummary Summary { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var user = await _users.GetUserAsync(User);
        DisplayName = user?.DisplayName ?? User.Identity?.Name ?? "there";
        var roles = user is null ? [] : await _users.GetRolesAsync(user);
        RoleName = string.Join(", ", roles.Select(Roles.DisplayName));
        Summary = await _monitoring.GetDashboardAsync(cancellationToken);
    }
}
