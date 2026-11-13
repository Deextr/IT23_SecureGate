using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Services;

namespace SecureGate1.Pages.Admin.MfaResets;

public class ReviewModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly MfaResetService _resets;
    private readonly StepUpGuard _stepUp;

    public ReviewModel(ApplicationDbContext db, MfaResetService resets, StepUpGuard stepUp)
    {
        _db = db;
        _resets = resets;
        _stepUp = stepUp;
    }

    public MfaResetRequest? RequestItem { get; private set; }

    public IActionResult OnGet(long id) =>
        RedirectToPage("/Admin/MfaResets/Index", new { request = id });

    public async Task<IActionResult> OnPostApproveAsync(long id, string? confirmation, string? decisionNotes, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(id);
        if (gate is not null)
            return gate;
        var result = await _resets.ApproveAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, confirmation, decisionNotes, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRejectAsync(
        long id,
        string? rejectionReason,
        string? rejectionOther,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(id);
        if (gate is not null)
            return gate;
        var result = await _resets.RejectAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            id,
            rejectionReason,
            rejectionOther,
            cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToPage("/Admin/MfaResets/Index", new { request = id });
    }

    private async Task LoadAsync(long id, CancellationToken cancellationToken)
    {
        RequestItem = await _db.MfaResetRequests.Include(r => r.TargetUser).SingleOrDefaultAsync(r => r.MfaResetRequestId == id, cancellationToken);
    }

    private async Task<IActionResult?> GateAsync(long id)
    {
        if (await _stepUp.IsFreshAsync(User))
            return null;
        return RedirectToPage("/Account/Reauthenticate", new { returnUrl = Url.Page("/Admin/MfaResets/Index", new { request = id })! });
    }
}
