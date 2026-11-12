using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.MfaResets;

public class RequestModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly MfaResetService _resets;

    public RequestModel(ApplicationDbContext db, MfaResetService resets)
    {
        _db = db;
        _resets = resets;
    }

    public IReadOnlyList<ApplicationUser> Employees { get; private set; } = [];

    [BindProperty]
    public string TargetUserId { get; set; } = "";

    [BindProperty]
    public string VerificationMethod { get; set; } = VerificationMethods.InPerson;

    [BindProperty]
    public string Notes { get; set; } = "";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Employees = await _db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await _resets.SubmitAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            TargetUserId,
            VerificationMethod,
            Notes,
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await OnGetAsync(cancellationToken);
            return Page();
        }

        TempData["Success"] = result.Message;
        return RedirectToPage("/Dashboard/Index");
    }
}
