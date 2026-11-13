using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.MfaResets;

public class EmployeeSearchModel : PageModel
{
    private readonly MfaResetService _resets;

    public EmployeeSearchModel(MfaResetService resets)
    {
        _resets = resets;
    }

    public async Task<IActionResult> OnGetAsync(string? q, CancellationToken cancellationToken)
    {
        if (!RecoveryAccess.CanSubmit(User))
            return Forbid();

        var items = await _resets.SearchEmployeesAsync(q, 15, cancellationToken);
        return new JsonResult(items);
    }
}
