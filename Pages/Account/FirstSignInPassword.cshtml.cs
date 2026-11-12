using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Account;

[AllowAnonymous]
[RequirePreAuth(PreAuthStages.ChangePassword)]
public class FirstSignInPasswordModel : PageModel
{
    private readonly SignInCompletionService _completion;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MfaService _mfa;
    private readonly AuditService _audit;
    private readonly SecurityPolicyOptions _policy;

    public FirstSignInPasswordModel(
        SignInCompletionService completion,
        UserManager<ApplicationUser> users,
        MfaService mfa,
        AuditService audit,
        IOptions<SecurityPolicyOptions> policy)
    {
        _completion = completion;
        _users = users;
        _mfa = mfa;
        _audit = audit;
        _policy = policy.Value;
    }

    [BindProperty]
    public string CurrentPassword { get; set; } = "";

    [BindProperty]
    public string NewPassword { get; set; } = "";

    [BindProperty]
    public string ConfirmPassword { get; set; } = "";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var user = await _completion.GetActivePreAuthUserAsync(PreAuthStages.ChangePassword);
        if (user is null)
            return RedirectToPage("Login");

        var passwordError = AccountRules.Password(NewPassword, _policy);
        if (passwordError is not null)
            ModelState.AddModelError(nameof(NewPassword), passwordError);
        if (NewPassword != ConfirmPassword)
            ModelState.AddModelError(nameof(ConfirmPassword), "The passwords do not match.");
        if (!ModelState.IsValid)
            return Page();

        var changed = await _users.ChangePasswordAsync(user, CurrentPassword, NewPassword);
        if (!changed.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "The password could not be changed. Check the temporary password and the new passphrase.");
            return Page();
        }

        user.MustChangePassword = false;
        await _users.UpdateAsync(user);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.PasswordChanged,
            Outcome = Outcomes.Success,
            ActorUserId = user.Id,
            TargetUserId = user.Id,
            Details = "Temporary password was replaced."
        }, cancellationToken);

        var enrolled = await _mfa.IsEnrolledAsync(user.Id, cancellationToken);
        await _completion.IssuePreAuthAsync(user, enrolled ? PreAuthStages.Verify : PreAuthStages.Enroll);
        return RedirectToPage("Continue");
    }
}
