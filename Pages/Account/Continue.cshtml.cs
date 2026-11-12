using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Security;

namespace SecureGate1.Pages.Account;

[AllowAnonymous]
public class ContinueModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        var auth = await HttpContext.AuthenticateAsync(AuthSchemes.PreAuth);
        var stage = auth.Principal?.FindFirst(AppClaims.PreAuthStage)?.Value;
        if (!auth.Succeeded || stage is null)
            return RedirectToPage("Login");

        return stage switch
        {
            PreAuthStages.ChangePassword => RedirectToPage("FirstSignInPassword"),
            PreAuthStages.Enroll => RedirectToPage("EnrollAuthenticator"),
            PreAuthStages.ShowRecoveryCodes => RedirectToPage("RecoveryCodes"),
            PreAuthStages.Verify => RedirectToPage("VerifyMfa"),
            _ => RedirectToPage("Login")
        };
    }
}
