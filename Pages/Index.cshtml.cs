using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Security;

namespace SecureGate1.Pages;

[AllowAnonymous]
public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true && User.HasClaim(AppClaims.Amr, AppClaims.Mfa))
            return RedirectToPage("/Dashboard/Index");
        return RedirectToPage("/Account/Login");
    }
}
