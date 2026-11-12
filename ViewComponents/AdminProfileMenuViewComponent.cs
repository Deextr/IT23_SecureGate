using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.ViewComponents;

public class AdminProfileMenuViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ProfilePhotoService _photos;
    private readonly MfaService _mfa;
    private readonly RecoveryCodeService _recovery;

    public AdminProfileMenuViewComponent(
        UserManager<ApplicationUser> users,
        ProfilePhotoService photos,
        MfaService mfa,
        RecoveryCodeService recovery)
    {
        _users = users;
        _photos = photos;
        _mfa = mfa;
        _recovery = recovery;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true)
            return Content(string.Empty);

        var user = await _users.GetUserAsync(HttpContext.User);
        var displayName = user?.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = user?.Email ?? HttpContext.User.Identity?.Name ?? "Account";

        var enrolled = user is not null && await _mfa.IsEnrolledAsync(user.Id);
        var unusedCodes = user is not null ? await _recovery.CountUnusedAsync(user.Id) : 0;
        var roleLabel = "User";
        if (user is not null)
        {
            var roles = await _users.GetRolesAsync(user);
            roleLabel = roles.Select(Roles.DisplayName).FirstOrDefault() ?? roleLabel;
        }

        var model = new AdminProfileMenuModel
        {
            DisplayName = displayName,
            Email = user?.Email ?? "",
            RoleLabel = roleLabel,
            Initials = ProfileAvatar.Initials(displayName),
            PhotoUrl = _photos.PublicUrl(user?.ProfilePhotoPath),
            HasPhoto = !string.IsNullOrEmpty(user?.ProfilePhotoPath),
            MfaEnrolled = enrolled,
            UnusedRecoveryCodes = unusedCodes,
            ReturnUrl = HttpContext.Request.Path
        };

        return View(model);
    }
}
