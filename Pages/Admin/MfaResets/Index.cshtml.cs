using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Admin.MfaResets;

public class IndexModel : PageModel
{
    private readonly MfaResetService _resets;
    private readonly StepUpGuard _stepUp;
    private readonly SecurityPolicyOptions _policy;

    public IndexModel(MfaResetService resets, StepUpGuard stepUp, IOptions<SecurityPolicyOptions> policy)
    {
        _resets = resets;
        _stepUp = stepUp;
        _policy = policy.Value;
    }

    public IReadOnlyList<MfaResetListItem> Requests { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "role")]
    public string? RoleFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "from")]
    public string? SubmittedFrom { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public string? SubmittedTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "request")]
    public long? OpenRequestId { get; set; }

    public int TotalPages { get; private set; } = 1;
    public int TotalCount { get; private set; }
    public int PageSize => _policy.PageSize;
    public int RangeStart => TotalCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
    public int RangeEnd => TotalCount == 0 ? 0 : Math.Min(TotalCount, PageNumber * PageSize);
    public bool CanSubmitRequests { get; private set; }
    public bool CanReviewRequests { get; private set; }
    public string? DateRangeError { get; private set; }
    public bool HasDateFilter => !string.IsNullOrWhiteSpace(SubmittedFrom) || !string.IsNullOrWhiteSpace(SubmittedTo);

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search)
        || !string.IsNullOrWhiteSpace(StatusFilter)
        || !string.IsNullOrWhiteSpace(RoleFilter)
        || HasDateFilter;

    [BindProperty]
    public string SubmitTargetUserId { get; set; } = "";

    [BindProperty]
    public string SubmitVerificationMethod { get; set; } = VerificationMethods.InPerson;

    [BindProperty]
    public List<string> SubmitChecks { get; set; } = [];

    [BindProperty]
    public string? SubmitOtherNotes { get; set; }

    public RecoverySubmitModalModel SubmitModal { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        PageNumber = Math.Max(1, PageNumber);
        CanSubmitRequests = RecoveryAccess.CanSubmit(User);
        CanReviewRequests = RecoveryAccess.CanManage(User);

        SubmitModal = BuildSubmitModal();

        if (!TryParseDateFilters(out var fromUtc, out var toExclusiveUtc))
        {
            Requests = [];
            TotalCount = 0;
            TotalPages = 1;
            return;
        }

        var status = NormalizeStatus(StatusFilter);
        var role = string.IsNullOrWhiteSpace(RoleFilter) ? null : RoleFilter;

        TotalCount = await _resets.CountAsync(Search, status, role, fromUtc, toExclusiveUtc, cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        if (PageNumber > TotalPages)
            PageNumber = TotalPages;

        Requests = await _resets.ListAsync(
            Search,
            status,
            role,
            fromUtc,
            toExclusiveUtc,
            PageNumber,
            PageSize,
            cancellationToken);
    }

    public async Task<IActionResult> OnPostSubmitAsync(CancellationToken cancellationToken)
    {
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
            return RedirectToList(submitOpen: true);
        }

        TempData["Success"] = result.Message;
        return RedirectToList();
    }

    public async Task<IActionResult> OnPostApproveAsync(long id, string? confirmation, string? decisionNotes, CancellationToken cancellationToken)
    {
        if (!RecoveryAccess.CanManage(User))
            return Forbid();

        var gate = await GateReviewAsync(id);
        if (gate is not null)
            return gate;

        var result = await _resets.ApproveAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            id,
            confirmation,
            decisionNotes,
            cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToList(requestId: id, keepReviewOpen: !result.Succeeded);
    }

    public async Task<IActionResult> OnPostRejectAsync(
        long id,
        string? rejectionReason,
        string? rejectionOther,
        CancellationToken cancellationToken)
    {
        if (!RecoveryAccess.CanManage(User))
            return Forbid();

        var gate = await GateReviewAsync(id);
        if (gate is not null)
            return gate;

        var result = await _resets.RejectAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            id,
            rejectionReason,
            rejectionOther,
            cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToList(requestId: id, keepReviewOpen: !result.Succeeded);
    }

    private RecoverySubmitModalModel BuildSubmitModal() =>
        new()
        {
            ErrorMessage = TempData["SubmitRequestError"] as string,
            PreserveFilters = new RecoverySubmitFilterState
            {
                Search = Search,
                Status = StatusFilter,
                Role = RoleFilter,
                From = SubmittedFrom,
                To = SubmittedTo,
                PageNumber = PageNumber
            }
        };

    private bool TryParseDateFilters(out DateTime? fromUtc, out DateTime? toExclusiveUtc)
    {
        fromUtc = null;
        toExclusiveUtc = null;
        DateOnly? from = null;
        DateOnly? to = null;

        if (!string.IsNullOrWhiteSpace(SubmittedFrom))
        {
            if (!DateOnly.TryParse(SubmittedFrom, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFrom))
            {
                DateRangeError = "From date is not valid.";
                return false;
            }

            from = parsedFrom;
        }

        if (!string.IsNullOrWhiteSpace(SubmittedTo))
        {
            if (!DateOnly.TryParse(SubmittedTo, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTo))
            {
                DateRangeError = "To date is not valid.";
                return false;
            }

            to = parsedTo;
        }

        if (from.HasValue && to.HasValue && to.Value < from.Value)
        {
            DateRangeError = "To date must be on or after the from date.";
            return false;
        }

        var tz = TimeZoneInfo.Local;
        if (from.HasValue)
        {
            var startLocal = DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            fromUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
        }

        if (to.HasValue)
        {
            var endLocal = DateTime.SpecifyKind(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            toExclusiveUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
        }

        return true;
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;
        return status switch
        {
            ResetRequestStatuses.Pending => ResetRequestStatuses.Pending,
            ResetRequestStatuses.Approved => ResetRequestStatuses.Approved,
            ResetRequestStatuses.Rejected => ResetRequestStatuses.Rejected,
            _ => null
        };
    }

    private IActionResult RedirectToList(bool submitOpen = false, long? requestId = null, bool keepReviewOpen = false)
    {
        if (submitOpen)
            TempData["OpenSubmitModal"] = true;

        return RedirectToPage(new
        {
            search = Search,
            status = StatusFilter,
            role = RoleFilter,
            from = SubmittedFrom,
            to = SubmittedTo,
            pageNumber = PageNumber,
            request = keepReviewOpen ? requestId : null,
            submit = submitOpen ? "open" : null
        });
    }

    private async Task<IActionResult?> GateReviewAsync(long id)
    {
        if (await _stepUp.IsFreshAsync(User))
            return null;
        return RedirectToPage("/Account/Reauthenticate", new { returnUrl = Url.Page("/Admin/MfaResets/Index", new { request = id })! });
    }
}
