using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Admin.Settings;

[EnableRateLimiting("auth-endpoints")]
public class IndexModel : PageModel
{
    private readonly SettingsService _settings;
    private readonly StepUpGuard _stepUp;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MfaService _mfa;
    private readonly SignInCompletionService _completion;

    public IndexModel(
        SettingsService settings,
        StepUpGuard stepUp,
        UserManager<ApplicationUser> users,
        MfaService mfa,
        SignInCompletionService completion,
        IOptions<SecurityPolicyOptions> policy)
    {
        _settings = settings;
        _stepUp = stepUp;
        _users = users;
        _mfa = mfa;
        _completion = completion;
        Policy = policy.Value;
    }

    public SecurityPolicyOptions Policy { get; }

    [BindProperty(SupportsGet = true)]
    public string Section { get; set; } = "overview";

    public string? LastUpdatedByName { get; private set; }

    [BindProperty]
    public SecuritySettings Input { get; set; } = new();

    [BindProperty]
    public string RowVersion { get; set; } = "";

    [BindProperty]
    public string Code { get; set; } = "";

    public bool RequiresStepUp { get; private set; }

    public string? StepUpError { get; private set; }

    public StepUpModalViewModel StepUpModal => new()
    {
        Required = RequiresStepUp,
        Error = StepUpError,
        Code = Code
    };

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await RefreshStepUpStateAsync(cancellationToken);
        if (RequiresStepUp)
            return Page();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (await NeedsStepUpAsync(cancellationToken))
        {
            RequiresStepUp = true;
            StepUpError ??= "Confirm your identity before saving changes.";
            return Page();
        }

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
            await LoadAsync(cancellationToken);
            return Page();
        }

        TempData["Success"] = result.Message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStepUpAsync(CancellationToken cancellationToken)
    {
        var user = await _users.GetUserAsync(User);
        if (user is not { IsActive: true })
            return RedirectToPage("/Account/Login");

        var result = await _mfa.VerifyAsync(user, Code, cancellationToken);
        if (result.RequiresAuthenticatorReset)
            return RedirectToPage("/Account/Login");

        if (!result.Succeeded)
        {
            StepUpError = result.Message;
            RequiresStepUp = true;
            return Page();
        }

        await _completion.RefreshStepUpAsync(user);
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Input = await _settings.GetAsync(cancellationToken);
        RowVersion = Convert.ToBase64String(Input.RowVersion);
        if (!string.IsNullOrEmpty(Input.UpdatedById))
        {
            var editor = await _users.FindByIdAsync(Input.UpdatedById);
            LastUpdatedByName = editor?.DisplayName ?? editor?.Email;
        }
    }

    private async Task<bool> NeedsStepUpAsync(CancellationToken cancellationToken) =>
        !await _stepUp.IsFreshAsync(User, cancellationToken);

    private async Task RefreshStepUpStateAsync(CancellationToken cancellationToken)
    {
        RequiresStepUp = !await _stepUp.IsFreshAsync(User, cancellationToken);
    }
}
