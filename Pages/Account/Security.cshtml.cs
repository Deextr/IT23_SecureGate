using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Account;

public class SecurityModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly MfaService _mfa;
    private readonly RecoveryCodeService _recovery;
    private readonly SignInCompletionService _completion;
    private readonly StepUpGuard _stepUp;
    private readonly AuditService _audit;
    private readonly SecurityPolicyOptions _policy;

    public SecurityModel(
        UserManager<ApplicationUser> users,
        MfaService mfa,
        RecoveryCodeService recovery,
        SignInCompletionService completion,
        StepUpGuard stepUp,
        AuditService audit,
        IOptions<SecurityPolicyOptions> policy)
    {
        _users = users;
        _mfa = mfa;
        _recovery = recovery;
        _completion = completion;
        _stepUp = stepUp;
        _audit = audit;
        _policy = policy.Value;
    }

    [BindProperty]
    public string CurrentPassword { get; set; } = "";

    [BindProperty]
    public string NewPassword { get; set; } = "";

    [BindProperty]
    public string ConfirmPassword { get; set; } = "";

    [BindProperty]
    public string? ReturnUrl { get; set; }

    public bool Enrolled { get; private set; }
    public int UnusedCodes { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
            return RedirectToPage("Login");
        Enrolled = await _mfa.IsEnrolledAsync(user.Id, cancellationToken);
        UnusedCodes = await _recovery.CountUnusedAsync(user.Id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostPasswordAsync(CancellationToken cancellationToken)
    {
        var gate = await GateAsync();
        if (gate is not null)
            return gate;
        var user = await _users.GetUserAsync(User);
        if (user is null)
            return RedirectToPage("Login");

        var passwordError = AccountRules.Password(NewPassword, _policy);
        if (passwordError is not null)
            ModelState.AddModelError(nameof(NewPassword), passwordError);
        if (NewPassword != ConfirmPassword)
            ModelState.AddModelError(nameof(ConfirmPassword), "The passwords do not match.");
        if (!ModelState.IsValid)
            return await FinishPasswordAsync(cancellationToken);

        var changed = await _users.ChangePasswordAsync(user, CurrentPassword, NewPassword);
        if (!changed.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "The password could not be changed.");
            return await FinishPasswordAsync(cancellationToken);
        }

        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.PasswordChanged,
            Outcome = Outcomes.Success,
            ActorUserId = user.Id,
            TargetUserId = user.Id,
            Details = "Password changed from security settings."
        }, cancellationToken);
        await _completion.RefreshStepUpAsync(user);
        TempData["Success"] = "Password changed. Other sessions for this account were signed out.";
        return RedirectAfterSecurityAction();
    }

    public async Task<IActionResult> OnPostRegenerateAsync(CancellationToken cancellationToken)
    {
        var gate = await GateAsync();
        if (gate is not null)
            return gate;
        var user = await _users.GetUserAsync(User);
        if (user is null)
            return RedirectToPage("Login");
        if (!await _mfa.IsEnrolledAsync(user.Id, cancellationToken))
        {
            TempData["Error"] = "Enroll an authenticator before creating recovery codes.";
            return RedirectAfterSecurityAction(openProfile: true);
        }

        await _recovery.RevokeUnusedAsync(user.Id, user.Id, cancellationToken);
        var codes = await _recovery.GenerateAsync(user.Id, user.Id, cancellationToken);
        TempData["RecoveryCodes"] = string.Join('\n', codes);
        return RedirectToPage("RecoveryCodes");
    }

    private async Task<IActionResult?> GateAsync()
    {
        if (await _stepUp.IsFreshAsync(User))
            return null;
        var back = LocalReturnUrl() ?? Url.Page("/Account/Security")!;
        return RedirectToPage("Reauthenticate", new { returnUrl = back });
    }

    private async Task<IActionResult> FinishPasswordAsync(CancellationToken cancellationToken)
    {
        if (LocalReturnUrl() is not null)
        {
            foreach (var entry in ModelState)
            {
                foreach (var error in entry.Value.Errors)
                {
                    if (string.IsNullOrEmpty(entry.Key))
                        TempData["PasswordFormError"] = error.ErrorMessage;
                    else
                        TempData[$"PasswordFieldError_{entry.Key}"] = error.ErrorMessage;
                }
            }
            TempData["OpenProfileModal"] = true;
            return RedirectAfterSecurityAction(openProfile: true);
        }

        return await OnGetAsync(cancellationToken);
    }

    private IActionResult RedirectAfterSecurityAction(bool openProfile = false)
    {
        var target = LocalReturnUrl();
        if (target is null)
            return RedirectToPage();

        if (openProfile)
            TempData["OpenProfileModal"] = true;

        var separator = target.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return LocalRedirect($"{target}{separator}profile=open");
    }

    private string? LocalReturnUrl()
    {
        if (string.IsNullOrWhiteSpace(ReturnUrl))
            return null;
        return Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;
    }
}
