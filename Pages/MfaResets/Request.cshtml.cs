using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Security;

namespace SecureGate1.Pages.MfaResets;

public class RequestModel : PageModel
{
    public IActionResult OnGet()
    {
        if (RecoveryAccess.CanManage(User))
            return RedirectToPage("/Admin/MfaResets/Index", new { submit = "open" });

        if (RecoveryAccess.CanSubmit(User))
            return RedirectToPage("/MfaResets/Index", new { submit = "open" });

        return RedirectToPage("/Dashboard/Index");
    }

    public IActionResult OnPost() => OnGet();
}
