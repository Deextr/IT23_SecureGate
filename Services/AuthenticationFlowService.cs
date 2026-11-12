using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;

namespace SecureGate1.Services;

public class AuthenticationFlowService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPasswordHasher<ApplicationUser> _hasher;
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly SettingsService _settings;
    private readonly TimeProvider _time;
    private readonly string _dummyHash;

    public AuthenticationFlowService(
        UserManager<ApplicationUser> users,
        IPasswordHasher<ApplicationUser> hasher,
        ApplicationDbContext db,
        AuditService audit,
        SettingsService settings,
        TimeProvider time)
    {
        _users = users;
        _hasher = hasher;
        _db = db;
        _audit = audit;
        _settings = settings;
        _time = time;
        _dummyHash = hasher.HashPassword(new ApplicationUser(), "timing-only-value");
    }

    public async Task<PasswordSignInResult> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var submitted = (email ?? "").Trim();
        if (submitted.Length is < 1 or > 254 || string.IsNullOrEmpty(password) || password.Length > 128)
        {
            BurnPasswordTime(password);
            await Task.Delay(AttemptDelay.Milliseconds(1), cancellationToken);
            return Failed();
        }

        var user = await _users.FindByNameAsync(submitted) ?? await _users.FindByEmailAsync(submitted);
        if (user is null)
        {
            BurnPasswordTime(password);
            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.PasswordFailed,
                Outcome = Outcomes.Failure,
                SubjectIdentifier = submitted,
                Details = "Password sign-in failed."
            }, cancellationToken);
            await Task.Delay(AttemptDelay.Milliseconds(1), cancellationToken);
            return Failed();
        }

        var now = _time.GetUtcNow();
        if (user.LockoutEnd is not null && user.LockoutEnd <= now)
        {
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            await _users.UpdateAsync(user);
        }

        if (user.LockoutEnd is not null && user.LockoutEnd > now)
        {
            BurnPasswordTime(password);
            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.SignInRejectedLocked,
                Outcome = Outcomes.Denied,
                Severity = Severities.Medium,
                TargetUserId = user.Id,
                SubjectIdentifier = submitted,
                Details = "Sign-in rejected because the account is locked."
            }, cancellationToken);
            return new PasswordSignInResult { Stage = PasswordStage.LockedOut, Message = SignInMessages.Locked, UserId = user.Id };
        }

        if (!user.IsActive)
        {
            BurnPasswordTime(password);
            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.SignInRejectedDisabled,
                Outcome = Outcomes.Denied,
                TargetUserId = user.Id,
                SubjectIdentifier = submitted,
                Details = "Sign-in rejected because the account is disabled."
            }, cancellationToken);
            await Task.Delay(AttemptDelay.Milliseconds(1), cancellationToken);
            return Failed();
        }

        if (!await _users.CheckPasswordAsync(user, password))
        {
            var settings = await _settings.GetAsync(cancellationToken);
            user.AccessFailedCount++;
            var locked = false;
            if (user.AccessFailedCount >= settings.LockoutFailedAttempts)
            {
                user.LockoutEnd = now.AddMinutes(settings.LockoutMinutes);
                locked = true;
            }

            await _users.UpdateAsync(user);
            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.PasswordFailed,
                Outcome = Outcomes.Failure,
                TargetUserId = user.Id,
                SubjectIdentifier = submitted,
                Details = "Password sign-in failed."
            }, cancellationToken);
            if (locked)
            {
                await _audit.WriteAsync(new AuditWrite
                {
                    EventType = EventTypes.AccountLocked,
                    Outcome = Outcomes.Denied,
                    Severity = Severities.High,
                    TargetUserId = user.Id,
                    SubjectIdentifier = submitted,
                    Details = "Account locked after repeated password failures."
                }, cancellationToken);
            }

            await Task.Delay(AttemptDelay.Milliseconds(user.AccessFailedCount), cancellationToken);
            return locked
                ? new PasswordSignInResult { Stage = PasswordStage.LockedOut, Message = SignInMessages.Locked, UserId = user.Id }
                : Failed();
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await _users.UpdateAsync(user);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.PasswordAccepted,
            Outcome = Outcomes.Success,
            ActorUserId = user.Id,
            TargetUserId = user.Id,
            SubjectIdentifier = submitted,
            Details = "Password accepted. A full session was not created."
        }, cancellationToken);

        if (user.MustChangePassword)
            return new PasswordSignInResult { Stage = PasswordStage.ChangePassword, UserId = user.Id };

        var enrolled = await _db.UserMfaSettings.AsNoTracking().AnyAsync(
            s => s.UserId == user.Id && s.IsEnabled && s.ProtectedSecret != null,
            cancellationToken);
        return new PasswordSignInResult
        {
            Stage = enrolled ? PasswordStage.Verify : PasswordStage.Enroll,
            UserId = user.Id
        };
    }

    private void BurnPasswordTime(string? password) =>
        _hasher.VerifyHashedPassword(new ApplicationUser(), _dummyHash, password ?? " ");

    private static PasswordSignInResult Failed() =>
        new() { Stage = PasswordStage.Failed, Message = SignInMessages.Generic };
}
