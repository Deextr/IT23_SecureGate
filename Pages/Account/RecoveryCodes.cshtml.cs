using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Account;

[AllowAnonymous]
public class RecoveryCodesModel : PageModel
{
    private readonly SignInCompletionService _completion;

    public RecoveryCodesModel(SignInCompletionService completion)
    {
        _completion = completion;
    }

    [BindProperty]
    public bool Acknowledged { get; set; }

    public IReadOnlyList<string> Codes { get; private set; } = [];
    public bool Missing { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CanViewAsync())
            return RedirectToPage("Login");
        LoadCodes();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await CanViewAsync())
            return RedirectToPage("Login");
        LoadCodes();
        if (!Acknowledged)
        {
            ModelState.AddModelError(nameof(Acknowledged), "Confirm that you have saved the codes, or that you understand they are no longer displayed.");
            TempData.Keep("RecoveryCodes");
            return Page();
        }

        TempData.Remove("RecoveryCodes");
        if (User.Identity?.IsAuthenticated == true && User.HasClaim(AppClaims.Amr, AppClaims.Mfa))
        {
            TempData["Success"] = "The new recovery codes are active. Older unused codes no longer work.";
            return RedirectToPage("Security");
        }

        var user = await _completion.GetActivePreAuthUserAsync(PreAuthStages.ShowRecoveryCodes);
        if (user is null)
            return RedirectToPage("Login");
        await _completion.CompleteAsync(user);
        return RedirectToPage("/Dashboard/Index");
    }

    private async Task<bool> CanViewAsync()
    {
        if (User.Identity?.IsAuthenticated == true && User.HasClaim(AppClaims.Amr, AppClaims.Mfa))
            return true;
        var auth = await HttpContext.AuthenticateAsync(AuthSchemes.PreAuth);
        return auth.Succeeded && auth.Principal?.FindFirst(AppClaims.PreAuthStage)?.Value == PreAuthStages.ShowRecoveryCodes;
    }

    private void LoadCodes()
    {
        var raw = TempData.Peek("RecoveryCodes") as string;
        if (string.IsNullOrWhiteSpace(raw))
        {
            Missing = true;
            Codes = [];
            return;
        }

        Codes = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
