using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Admin.Users;

public class CreateModel : PageModel
{
    private readonly UserAdministrationService _users;
    private readonly StepUpGuard _stepUp;

    public CreateModel(UserAdministrationService users, StepUpGuard stepUp)
    {
        _users = users;
        _stepUp = stepUp;
    }

    [BindProperty]
    public string DisplayName { get; set; } = "";

    [BindProperty]
    public string Email { get; set; } = "";

    [BindProperty]
    public string Role { get; set; } = Roles.Employee;

    [BindProperty]
    public string? Confirmation { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var gate = await GateAsync();
        return gate ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var gate = await GateAsync();
        if (gate is not null)
            return gate;

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _users.CreateAsync(actorId, Email, DisplayName, Role, Confirmation, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }

        TempData["TemporaryPassword"] = result.TemporaryPassword;
        TempData["Success"] = result.Message;
        TempData["OpenAccountId"] = result.UserId;
        return RedirectToPage("Index", new { account = result.UserId });
    }

    private async Task<IActionResult?> GateAsync()
    {
        if (await _stepUp.IsFreshAsync(User))
            return null;
        return RedirectToPage("/Account/Reauthenticate", new { returnUrl = Url.Page("/Admin/Users/Create") });
    }
}
