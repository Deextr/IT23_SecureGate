using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("auth-endpoints")]
[RequirePreAuth(PreAuthStages.Enroll)]
public class EnrollAuthenticatorModel : PageModel
{
    private readonly SignInCompletionService _completion;
    private readonly MfaService _mfa;

    public EnrollAuthenticatorModel(SignInCompletionService completion, MfaService mfa)
    {
        _completion = completion;
        _mfa = mfa;
    }

    [BindProperty]
    public string ProtectedPayload { get; set; } = "";

    [BindProperty]
    public string Code { get; set; } = "";

    public string QrDataUri { get; private set; } = "";
    public string ManualKey { get; private set; } = "";
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _completion.GetActivePreAuthUserAsync(PreAuthStages.Enroll);
        if (user is null)
            return RedirectToPage("Login");
        ShowChallenge(_mfa.BeginEnrollment(user.Id, user.Email ?? user.Id));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var user = await _completion.GetActivePreAuthUserAsync(PreAuthStages.Enroll);
        if (user is null)
            return RedirectToPage("Login");

        if (!_mfa.TryReadEnrollment(user.Id, ProtectedPayload, out var secret))
        {
            Error = "This setup session expired. A new code was created. Scan it again.";
            ShowChallenge(_mfa.BeginEnrollment(user.Id, user.Email ?? user.Id));
            return Page();
        }

        var (result, codes) = await _mfa.ConfirmEnrollmentAsync(user, ProtectedPayload, Code, cancellationToken);
        if (!result.Succeeded)
        {
            Error = result.Message;
            ManualKey = Base32.Group(Base32.Encode(secret));
            QrDataUri = _mfa.QrForSecret(user.Email ?? user.Id, secret);
            return Page();
        }

        TempData["RecoveryCodes"] = string.Join('\n', codes);
        await _completion.IssuePreAuthAsync(user, PreAuthStages.ShowRecoveryCodes);
        return RedirectToPage("RecoveryCodes");
    }

    private void ShowChallenge(EnrollmentChallenge challenge)
    {
        ProtectedPayload = challenge.ProtectedPayload;
        ManualKey = challenge.ManualKey;
        QrDataUri = challenge.QrDataUri;
    }
}
