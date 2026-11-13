using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("auth-endpoints")]
public class LoginModel : PageModel
{
    private readonly AuthenticationFlowService _flow;
    private readonly SignInCompletionService _completion;
    private readonly UserManager<ApplicationUser> _users;

    public LoginModel(AuthenticationFlowService flow, SignInCompletionService completion, UserManager<ApplicationUser> users)
    {
        _flow = flow;
        _completion = completion;
        _users = users;
    }

    [BindProperty]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254)]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [BindProperty]
    [StringLength(128, MinimumLength = 1)]
    public string Password { get; set; } = "";

    public string? Error { get; private set; }

    public bool ShowLockout { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true && User.HasClaim(AppClaims.Amr, AppClaims.Mfa))
            return RedirectToPage("/Dashboard/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Error = SignInMessages.Generic;
            return Page();
        }

        var result = await _flow.CheckPasswordAsync(Email, Password, cancellationToken);
        if (result.Stage == PasswordStage.Failed)
        {
            Error = result.Message;
            return Page();
        }

        if (result.Stage == PasswordStage.LockedOut)
        {
            ShowLockout = true;
            return Page();
        }

        var account = await _users.FindByIdAsync(result.UserId!);
        if (account is null)
        {
            Error = SignInMessages.Generic;
            return Page();
        }

        var stage = result.Stage switch
        {
            PasswordStage.ChangePassword => PreAuthStages.ChangePassword,
            PasswordStage.Enroll => PreAuthStages.Enroll,
            _ => PreAuthStages.Verify
        };
        await _completion.IssuePreAuthAsync(account, stage);
        return RedirectToPage("Continue");
    }
}
