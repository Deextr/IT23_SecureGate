using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Account;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly AuditService _audit;

    public LogoutModel(SignInManager<ApplicationUser> signIn, AuditService audit)
    {
        _signIn = signIn;
        _audit = audit;
    }

    public IActionResult OnGet() => RedirectToPage("Login");

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.Logout,
                Outcome = Outcomes.Success,
                ActorUserId = userId,
                TargetUserId = userId,
                Details = "User signed out."
            }, cancellationToken);
        }

        await _signIn.SignOutAsync();
        await HttpContext.SignOutAsync(AuthSchemes.PreAuth);
        return RedirectToPage("Login");
    }
}
