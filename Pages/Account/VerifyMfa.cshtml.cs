using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("auth-endpoints")]
[RequirePreAuth(PreAuthStages.Verify)]
public class VerifyMfaModel : PageModel
{
    private readonly SignInCompletionService _completion;
    private readonly MfaService _mfa;

    public VerifyMfaModel(SignInCompletionService completion, MfaService mfa)
    {
        _completion = completion;
        _mfa = mfa;
    }

    [BindProperty]
    public string Code { get; set; } = "";

    public string? Error { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var user = await _completion.GetActivePreAuthUserAsync(PreAuthStages.Verify);
        if (user is null)
            return RedirectToPage("Login");

        var result = await _mfa.VerifyAsync(user, Code, cancellationToken);
        if (!result.Succeeded)
        {
            Error = result.Message;
            return Page();
        }

        await _completion.CompleteAsync(user);
        return RedirectToPage("/Dashboard/Index");
    }
}
