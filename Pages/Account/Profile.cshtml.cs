using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Pages.Account;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ProfilePhotoService _photos;
    private readonly AuditService _audit;
    private readonly SignInCompletionService _completion;
    private readonly StepUpGuard _stepUp;
    private readonly SecurityPolicyOptions _policy;

    public ProfileModel(
        UserManager<ApplicationUser> users,
        ProfilePhotoService photos,
        AuditService audit,
        SignInCompletionService completion,
        StepUpGuard stepUp,
        IOptions<SecurityPolicyOptions> policy)
    {
        _users = users;
        _photos = photos;
        _audit = audit;
        _completion = completion;
        _stepUp = stepUp;
        _policy = policy.Value;
    }

    [BindProperty]
    public string DisplayName { get; set; } = "";

    [BindProperty]
    public IFormFile? Photo { get; set; }

    [BindProperty]
    public bool RemovePhoto { get; set; }

    [BindProperty]
    public string CurrentPassword { get; set; } = "";

    [BindProperty]
    public string NewPassword { get; set; } = "";

    [BindProperty]
    public string ConfirmPassword { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => RedirectToPage("/Dashboard/Index");

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
            return RedirectToPage("Login");

        var nameError = AccountRules.DisplayName(DisplayName);
        if (nameError is not null)
            return FailProfile(nameError);

        var passwordAttempt = !string.IsNullOrWhiteSpace(CurrentPassword)
            || !string.IsNullOrWhiteSpace(NewPassword)
            || !string.IsNullOrWhiteSpace(ConfirmPassword);

        if (passwordAttempt)
        {
            if (!await _stepUp.IsFreshAsync(User))
            {
                var back = LocalReturnUrl() ?? Url.Page("/Dashboard/Index")!;
                return RedirectToPage("Reauthenticate", new { returnUrl = back });
            }

            var passwordError = AccountRules.Password(NewPassword, _policy);
            if (passwordError is not null)
                return FailPasswordField(nameof(NewPassword), passwordError);
            if (NewPassword != ConfirmPassword)
                return FailPasswordField(nameof(ConfirmPassword), "The passwords do not match.");
            if (string.IsNullOrWhiteSpace(CurrentPassword))
                return FailPasswordField(nameof(CurrentPassword), "Enter your current password.");

            var changed = await _users.ChangePasswordAsync(user, CurrentPassword, NewPassword);
            if (!changed.Succeeded)
                return FailProfile("The password could not be changed.");

            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.PasswordChanged,
                Outcome = Outcomes.Success,
                ActorUserId = user.Id,
                TargetUserId = user.Id,
                Details = "Password changed from profile settings."
            }, cancellationToken);
            await _completion.RefreshStepUpAsync(user);
        }

        var trimmed = DisplayName.Trim();
        var nameChanged = !string.Equals(user.DisplayName, trimmed, StringComparison.Ordinal);
        user.DisplayName = trimmed;

        if (RemovePhoto)
        {
            _photos.Delete(user.Id);
            user.ProfilePhotoPath = null;
        }
        else if (Photo is { Length: > 0 })
        {
            var (path, photoError) = await _photos.SaveAsync(user.Id, Photo, cancellationToken);
            if (photoError is not null)
                return FailProfile(photoError);

            if (path is not null)
                user.ProfilePhotoPath = path;
        }

        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
            return FailProfile("Your profile could not be updated.");

        if (nameChanged || RemovePhoto || Photo is { Length: > 0 })
        {
            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.ProfileUpdated,
                Outcome = Outcomes.Success,
                ActorUserId = user.Id,
                TargetUserId = user.Id,
                Details = "Profile display name or photo updated."
            }, cancellationToken);
        }

        TempData["Success"] = passwordAttempt
            ? "Profile and password updated. Other sessions for this account were signed out."
            : "Profile updated.";
        return RedirectBack();
    }

    private IActionResult FailProfile(string message)
    {
        TempData["Error"] = message;
        TempData["OpenProfileModal"] = true;
        return RedirectBack(openProfile: true);
    }

    private IActionResult FailPasswordField(string field, string message)
    {
        if (string.IsNullOrEmpty(field))
            TempData["PasswordFormError"] = message;
        else
            TempData[$"PasswordFieldError_{field}"] = message;
        TempData["OpenProfileModal"] = true;
        return RedirectBack(openProfile: true);
    }

    private IActionResult RedirectBack(bool openProfile = false)
    {
        var target = LocalReturnUrl() ?? "/Dashboard";
        if (openProfile)
            TempData["OpenProfileModal"] = true;
        var separator = target.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var suffix = openProfile ? $"{separator}profile=open" : "";
        return LocalRedirect($"{target}{suffix}");
    }

    private string? LocalReturnUrl()
    {
        if (string.IsNullOrWhiteSpace(ReturnUrl))
            return null;
        return Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;
    }
}
