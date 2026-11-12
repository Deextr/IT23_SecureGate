using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using SecureGate1.Models;
using SecureGate1.Security;

namespace SecureGate1.Services;

public class SignInCompletionService
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IHttpContextAccessor _http;
    private readonly TimeProvider _time;

    public SignInCompletionService(
        SignInManager<ApplicationUser> signIn,
        UserManager<ApplicationUser> users,
        IHttpContextAccessor http,
        TimeProvider time)
    {
        _signIn = signIn;
        _users = users;
        _http = http;
        _time = time;
    }

    public async Task IssuePreAuthAsync(ApplicationUser user, string stage)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.DisplayName ?? user.Email ?? user.Id),
            new(AppClaims.PreAuthStage, stage)
        };
        var identity = new ClaimsIdentity(claims, AuthSchemes.PreAuth);
        await _http.HttpContext!.SignInAsync(AuthSchemes.PreAuth, new ClaimsPrincipal(identity));
    }

    public async Task CompleteAsync(ApplicationUser user)
    {
        var http = _http.HttpContext ?? throw new InvalidOperationException("No active request.");
        await http.SignOutAsync(AuthSchemes.PreAuth);
        user.LastSignInAtUtc = _time.GetUtcNow().UtcDateTime;
        await _users.UpdateAsync(user);
        var now = _time.GetUtcNow().ToUnixTimeSeconds().ToString();
        await _signIn.SignInWithClaimsAsync(user, isPersistent: false,
        [
            new Claim(AppClaims.Amr, AppClaims.Mfa),
            new Claim(AppClaims.AuthTime, now),
            new Claim(AppClaims.MfaTime, now)
        ]);
    }

    public async Task<ApplicationUser?> GetActivePreAuthUserAsync(params string[] stages)
    {
        var http = _http.HttpContext ?? throw new InvalidOperationException("No active request.");
        var auth = await http.AuthenticateAsync(AuthSchemes.PreAuth);
        if (!auth.Succeeded)
            return null;

        var stage = auth.Principal?.FindFirst(AppClaims.PreAuthStage)?.Value;
        if (stages.Length > 0 && (stage is null || !stages.Contains(stage)))
            return null;

        var id = auth.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id is null ? null : await _users.FindByIdAsync(id);
        if (user is not { IsActive: true })
        {
            await http.SignOutAsync(AuthSchemes.PreAuth);
            return null;
        }

        return user;
    }

    public async Task RefreshStepUpAsync(ApplicationUser user)
    {
        var http = _http.HttpContext ?? throw new InvalidOperationException("No active request.");
        var authTime = http.User.FindFirst(AppClaims.AuthTime)?.Value ?? _time.GetUtcNow().ToUnixTimeSeconds().ToString();
        var now = _time.GetUtcNow().ToUnixTimeSeconds().ToString();
        await _signIn.SignInWithClaimsAsync(user, isPersistent: false,
        [
            new Claim(AppClaims.Amr, AppClaims.Mfa),
            new Claim(AppClaims.AuthTime, authTime),
            new Claim(AppClaims.MfaTime, now)
        ]);
    }
}

public class StepUpGuard
{
    private readonly SettingsService _settings;
    private readonly TimeProvider _time;

    public StepUpGuard(SettingsService settings, TimeProvider time)
    {
        _settings = settings;
        _time = time;
    }

    public async Task<bool> IsFreshAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var value = user.FindFirst(AppClaims.MfaTime)?.Value;
        if (!long.TryParse(value, out var unix))
            return false;
        var at = DateTimeOffset.FromUnixTimeSeconds(unix);
        return _time.GetUtcNow() - at <= TimeSpan.FromMinutes(settings.StepUpMinutes);
    }
}
