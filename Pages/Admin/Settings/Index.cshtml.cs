using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Admin.Settings;

public class IndexModel : PageModel
{
    private readonly SettingsService _settings;
    private readonly StepUpGuard _stepUp;

    public IndexModel(SettingsService settings, StepUpGuard stepUp, IOptions<SecurityPolicyOptions> policy)
    {
        _settings = settings;
        _stepUp = stepUp;
        Policy = policy.Value;
    }

    public SecurityPolicyOptions Policy { get; }

    [BindProperty]
    public SecuritySettings Input { get; set; } = new();

    [BindProperty]
    public string RowVersion { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var gate = await GateAsync();
        if (gate is not null)
            return gate;
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var gate = await GateAsync();
        if (gate is not null)
            return gate;

        byte[] version;
        try
        {
            version = Convert.FromBase64String(RowVersion);
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "The settings form expired. Reload the page.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await _settings.UpdateAsync(Input, version, User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }

        TempData["Success"] = result.Message;
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Input = await _settings.GetAsync(cancellationToken);
        RowVersion = Convert.ToBase64String(Input.RowVersion);
    }

    private async Task<IActionResult?> GateAsync()
    {
        if (await _stepUp.IsFreshAsync(User))
            return null;
        return RedirectToPage("/Account/Reauthenticate", new { returnUrl = Url.Page("/Admin/Settings/Index") });
    }
}
