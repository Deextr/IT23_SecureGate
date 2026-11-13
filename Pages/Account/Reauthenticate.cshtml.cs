using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SecureGate1.Models;
using SecureGate1.Services;

namespace SecureGate1.Pages.Account;

[EnableRateLimiting("auth-endpoints")]
public class ReauthenticateModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly MfaService _mfa;
    private readonly SignInCompletionService _completion;

    public ReauthenticateModel(UserManager<ApplicationUser> users, MfaService mfa, SignInCompletionService completion)
    {
        _users = users;
        _mfa = mfa;
        _completion = completion;
    }

    [BindProperty]
    public string Code { get; set; } = "";

    [BindProperty]
    public string ReturnUrl { get; set; } = "/Dashboard";

    public string? Error { get; private set; }

    public void OnGet(string? returnUrl)
    {
        ReturnUrl = Local(returnUrl);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ReturnUrl = Local(ReturnUrl);
        var user = await _users.GetUserAsync(User);
        if (user is not { IsActive: true })
            return RedirectToPage("Login");

        var result = await _mfa.VerifyAsync(user, Code, cancellationToken);
        if (result.RequiresAuthenticatorReset)
            return RedirectToPage("Login");

        if (!result.Succeeded)
        {
            Error = result.Message;
            return Page();
        }

        await _completion.RefreshStepUpAsync(user);
        return LocalRedirect(ReturnUrl);
    }

    private string Local(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : (Url.Page("/Dashboard/Index") ?? "/Dashboard");
}
