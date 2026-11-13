using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.MfaResets;

public class IndexModel : PageModel
{
    private readonly MfaResetService _resets;

    public IndexModel(MfaResetService resets)
    {
        _resets = resets;
    }

    [BindProperty]
    public string SubmitTargetUserId { get; set; } = "";

    [BindProperty]
    public string SubmitVerificationMethod { get; set; } = VerificationMethods.InPerson;

    [BindProperty]
    public List<string> SubmitChecks { get; set; } = [];

    [BindProperty]
    public string? SubmitOtherNotes { get; set; }

    public IActionResult OnGet()
    {
        if (RecoveryAccess.CanManage(User))
            return RedirectToPage("/Admin/MfaResets/Index");

        if (!RecoveryAccess.CanSubmit(User))
            return RedirectToPage("/Dashboard/Index");

        if (string.Equals(Request.Query["submit"], "open", StringComparison.OrdinalIgnoreCase))
            return Page();

        return Page();
    }

    public async Task<IActionResult> OnPostSubmitAsync(CancellationToken cancellationToken)
    {
        if (RecoveryAccess.CanManage(User))
            return Forbid();

        if (!RecoveryAccess.CanSubmit(User))
            return Forbid();

        var result = await _resets.SubmitAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            SubmitTargetUserId,
            SubmitVerificationMethod,
            SubmitChecks,
            SubmitOtherNotes,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["SubmitRequestError"] = result.Message;
            TempData["OpenSubmitModal"] = true;
            return RedirectToPage(new { submit = "open" });
        }

        TempData["Success"] = result.Message;
        return RedirectToPage();
    }
}
