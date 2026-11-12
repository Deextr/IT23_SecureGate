using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Admin.Users;

public class IndexModel : PageModel
{
    private readonly UserAdministrationService _users;
    private readonly StepUpGuard _stepUp;
    private readonly SecurityPolicyOptions _policy;

    public IndexModel(UserAdministrationService users, StepUpGuard stepUp, IOptions<SecurityPolicyOptions> policy)
    {
        _users = users;
        _stepUp = stepUp;
        _policy = policy.Value;
    }

    public IReadOnlyList<UserListItem> Users { get; private set; } = [];
    public IReadOnlyDictionary<string, string> Roles { get; private set; } = new Dictionary<string, string>();
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "role")]
    public string? RoleFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Account { get; set; }

    public int TotalPages { get; private set; } = 1;
    public int TotalCount { get; private set; }
    public int PageSize => _policy.PageSize;
    public int RangeStart => TotalCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
    public int RangeEnd => TotalCount == 0 ? 0 : Math.Min(TotalCount, PageNumber * PageSize);
    public string ActorUserId { get; private set; } = "";
    public bool ActorIsSuperadmin { get; private set; }

    [BindProperty]
    public string CreateDisplayName { get; set; } = "";

    [BindProperty]
    public string CreateEmail { get; set; } = "";

    [BindProperty]
    public string CreateRole { get; set; } = Security.Roles.Employee;

    [BindProperty]
    public string? CreateConfirmation { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        ActorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        ActorIsSuperadmin = User.IsInRole(Security.Roles.Superadmin);
        PageNumber = Math.Max(1, PageNumber);
        var role = string.IsNullOrWhiteSpace(RoleFilter) ? null : RoleFilter;
        var status = string.IsNullOrWhiteSpace(StatusFilter) ? null : StatusFilter;
        TotalCount = await _users.CountAsync(Search, role, status, cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        if (PageNumber > TotalPages)
            PageNumber = TotalPages;
        Users = await _users.ListAsync(Search, role, status, PageNumber, PageSize, cancellationToken);
        Roles = await _users.RolesForAsync(Users.Select(u => u.Id), cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var gate = await GateCreateAsync();
        if (gate is not null)
            return gate;

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _users.CreateAsync(
            actorId,
            CreateEmail,
            CreateDisplayName,
            CreateRole,
            CreateConfirmation,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["CreateAccountError"] = result.Message;
            TempData["OpenCreateAdminModal"] = true;
            return RedirectToPage(new
            {
                search = Search,
                role = RoleFilter,
                status = StatusFilter,
                pageNumber = PageNumber,
                create = "open"
            });
        }

        TempData["TemporaryPassword"] = result.TemporaryPassword;
        TempData["Success"] = result.Message;
        return RedirectToAccountList(result.UserId);
    }

    public async Task<IActionResult> OnPostRoleAsync(string id, string newRole, string? confirmation, CancellationToken cancellationToken)
    {
        var gate = await GateAccountAsync(id);
        if (gate is not null)
            return gate;

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _users.ChangeRoleAsync(actorId, id, newRole, confirmation, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAccountList(id);
    }

    public async Task<IActionResult> OnPostStatusAsync(string id, bool active, string? confirmation, CancellationToken cancellationToken)
    {
        var gate = await GateAccountAsync(id);
        if (gate is not null)
            return gate;

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _users.SetActiveAsync(actorId, id, active, confirmation, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAccountList(id);
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id, CancellationToken cancellationToken)
    {
        var gate = await GateAccountAsync(id);
        if (gate is not null)
            return gate;

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _users.ResetPasswordAsync(actorId, id, cancellationToken);
        if (!result.Succeeded)
            TempData["Error"] = result.Message;
        else
        {
            TempData["TemporaryPassword"] = result.TemporaryPassword;
            TempData["Success"] = result.Message;
        }

        return RedirectToAccountList(id);
    }

    private IActionResult RedirectToAccountList(string? accountId = null)
    {
        if (!string.IsNullOrEmpty(accountId))
            TempData["OpenAccountId"] = accountId;

        return RedirectToPage(new
        {
            search = Search,
            role = RoleFilter,
            status = StatusFilter,
            pageNumber = PageNumber,
            account = accountId
        });
    }

    private async Task<IActionResult?> GateCreateAsync()
    {
        if (await _stepUp.IsFreshAsync(User))
            return null;
        return RedirectToPage("/Account/Reauthenticate", new { returnUrl = Url.Page("/Admin/Users/Index", new { create = "open" })! });
    }

    private async Task<IActionResult?> GateAccountAsync(string id)
    {
        if (await _stepUp.IsFreshAsync(User))
            return null;
        return RedirectToPage("/Account/Reauthenticate", new { returnUrl = Url.Page("/Admin/Users/Index", new { account = id })! });
    }
}
