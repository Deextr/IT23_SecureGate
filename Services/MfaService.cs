using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;

namespace SecureGate1.Services;

public sealed class EnrollmentChallenge
{
    public required string ProtectedPayload { get; init; }
    public required string ManualKey { get; init; }
    public required string QrDataUri { get; init; }
}

public class MfaService
{
    public const string SecretPurpose = "SecureGate.TotpSecret.v1";
    public const string EnrollmentPurpose = "SecureGate.Enrollment.v1";

    private readonly ApplicationDbContext _db;
    private readonly TotpService _totp;
    private readonly RecoveryCodeService _recovery;
    private readonly AuditService _audit;
    private readonly FailureWindow _window;
    private readonly SettingsService _settings;
    private readonly TimeProvider _time;
    private readonly IDataProtector _secrets;
    private readonly IDataProtector _enrollment;

    public MfaService(
        ApplicationDbContext db,
        TotpService totp,
        RecoveryCodeService recovery,
        AuditService audit,
        FailureWindow window,
        SettingsService settings,
        TimeProvider time,
        IDataProtectionProvider dataProtection)
    {
        _db = db;
        _totp = totp;
        _recovery = recovery;
        _audit = audit;
        _window = window;
        _settings = settings;
        _time = time;
        _secrets = dataProtection.CreateProtector(SecretPurpose);
        _enrollment = dataProtection.CreateProtector(EnrollmentPurpose);
    }

    public async Task<bool> IsEnrolledAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserMfaSettings.AnyAsync(
            s => s.UserId == userId && s.IsEnabled && s.ProtectedSecret != null,
            cancellationToken);
    }

    public EnrollmentChallenge BeginEnrollment(string userId, string accountName)
    {
        var secret = _totp.CreateSecret();
        var base32 = Base32.Encode(secret);
        var expires = _time.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds();
        var payload = _enrollment.Protect($"{userId}\n{expires}\n{base32}");
        var uri = _totp.BuildProvisioningUri(accountName, base32);
        return new EnrollmentChallenge
        {
            ProtectedPayload = payload,
            ManualKey = Base32.Group(base32),
            QrDataUri = QrCodeEncoder.ToDataUri(uri)
        };
    }

    public bool TryReadEnrollment(string userId, string protectedPayload, out byte[] secret)
    {
        secret = [];
        try
        {
            var text = _enrollment.Unprotect(protectedPayload);
            var parts = text.Split('\n');
            if (parts.Length != 3 || !string.Equals(parts[0], userId, StringComparison.Ordinal))
                return false;
            if (!long.TryParse(parts[1], out var expiresUnix))
                return false;
            if (DateTimeOffset.FromUnixTimeSeconds(expiresUnix) < _time.GetUtcNow())
                return false;
            if (!Base32.TryDecode(parts[2], out secret))
                return false;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public string QrForSecret(string accountName, byte[] secret)
    {
        var uri = _totp.BuildProvisioningUri(accountName, Base32.Encode(secret));
        return QrCodeEncoder.ToDataUri(uri);
    }

    public async Task<(ServiceResult Result, IReadOnlyList<string> Codes)> ConfirmEnrollmentAsync(
        ApplicationUser user,
        string protectedPayload,
        string code,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        if (await _window.InCooldownAsync(user.Id, EventTypes.TotpCooldown, settings.TotpCooldownMinutes, cancellationToken))
        {
            return (ServiceResult.Fail(SignInMessages.Cooldown), []);
        }

        if (!TryReadEnrollment(user.Id, protectedPayload, out var secret))
            return (ServiceResult.Fail("This setup session expired. Start the authenticator setup again."), []);

        var matched = _totp.Verify(secret, code, _time.GetUtcNow(), lastAcceptedTimeStep: null);
        if (matched is null)
        {
            await RegisterTotpFailureAsync(user.Id, settings, "Enrollment confirmation failed.", cancellationToken);
            return (ServiceResult.Fail(SignInMessages.EnrollmentFailed), []);
        }

        var row = await _db.UserMfaSettings.SingleOrDefaultAsync(s => s.UserId == user.Id, cancellationToken);
        var now = _time.GetUtcNow().UtcDateTime;
        if (row is null)
        {
            row = new UserMfaSetting { UserId = user.Id };
            _db.UserMfaSettings.Add(row);
        }

        row.ProtectedSecret = _secrets.Protect(Convert.ToBase64String(secret));
        row.IsEnabled = true;
        row.KeyVersion = 1;
        row.EnrolledAtUtc = now;
        row.LastVerifiedAtUtc = now;
        row.LastAcceptedTimeStep = matched;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.MfaEnrollmentConfirmed,
            Outcome = Outcomes.Success,
            ActorUserId = user.Id,
            TargetUserId = user.Id,
            Details = "Authenticator enrollment was confirmed."
        }, cancellationToken);

        var codes = await _recovery.GenerateAsync(user.Id, user.Id, cancellationToken);
        return (ServiceResult.Ok("Authenticator enrolled."), codes);
    }

    public async Task<ServiceResult> VerifyAsync(ApplicationUser user, string code, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        if (await _window.InCooldownAsync(user.Id, EventTypes.TotpCooldown, settings.TotpCooldownMinutes, cancellationToken))
            return ServiceResult.Fail(SignInMessages.Cooldown);

        var row = await _db.UserMfaSettings.SingleOrDefaultAsync(s => s.UserId == user.Id && s.IsEnabled, cancellationToken);
        if (row?.ProtectedSecret is null)
            return ServiceResult.Fail("Multi-factor authentication is not enrolled for this account.");

        byte[] secret;
        try
        {
            secret = Convert.FromBase64String(_secrets.Unprotect(row.ProtectedSecret));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return ServiceResult.Fail("Multi-factor authentication is unavailable for this account. Ask an administrator to reset it.");
        }

        var matched = _totp.Verify(secret, code, _time.GetUtcNow(), row.LastAcceptedTimeStep);
        if (matched is null)
        {
            await RegisterTotpFailureAsync(user.Id, settings, "Authenticator verification failed.", cancellationToken);
            return ServiceResult.Fail(SignInMessages.MfaFailed);
        }

        row.LastAcceptedTimeStep = matched;
        row.LastVerifiedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.TotpSucceeded,
            Outcome = Outcomes.Success,
            ActorUserId = user.Id,
            TargetUserId = user.Id,
            Details = "Authenticator verification succeeded."
        }, cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RedeemRecoveryCodeAsync(ApplicationUser user, string code, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        if (await _window.InCooldownAsync(user.Id, EventTypes.RecoveryCooldown, settings.RecoveryCooldownMinutes, cancellationToken))
            return ServiceResult.Fail(SignInMessages.Cooldown);

        var redeemed = await _recovery.RedeemAsync(user.Id, code, cancellationToken);
        if (!redeemed)
        {
            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.RecoveryCodeFailed,
                Outcome = Outcomes.Failure,
                ActorUserId = user.Id,
                TargetUserId = user.Id,
                Details = "Recovery code was rejected."
            }, cancellationToken);
            var count = await _window.CountSinceAsync(user.Id, EventTypes.RecoveryCodeFailed, settings.RecoveryFailureWindowMinutes, cancellationToken);
            if (count >= settings.RecoveryFailureLimit)
            {
                await _audit.WriteAsync(new AuditWrite
                {
                    EventType = EventTypes.RecoveryCooldown,
                    Outcome = Outcomes.Denied,
                    Severity = Severities.High,
                    ActorUserId = user.Id,
                    TargetUserId = user.Id,
                    Details = "Recovery-code attempts reached the cooldown threshold."
                }, cancellationToken);
            }

            return ServiceResult.Fail(SignInMessages.RecoveryFailed);
        }

        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.RecoveryCodeSucceeded,
            Outcome = Outcomes.Success,
            Severity = Severities.High,
            ActorUserId = user.Id,
            TargetUserId = user.Id,
            Details = "A recovery code was accepted and consumed."
        }, cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task ClearBindingAsync(string userId, CancellationToken cancellationToken = default)
    {
        var row = await _db.UserMfaSettings.SingleOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (row is null)
            return;
        row.ProtectedSecret = null;
        row.IsEnabled = false;
        row.LastAcceptedTimeStep = null;
        row.LastVerifiedAtUtc = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task RegisterTotpFailureAsync(string userId, SecuritySettings settings, string details, CancellationToken cancellationToken)
    {
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.TotpFailed,
            Outcome = Outcomes.Failure,
            ActorUserId = userId,
            TargetUserId = userId,
            Details = details
        }, cancellationToken);
        var count = await _window.CountSinceAsync(userId, EventTypes.TotpFailed, settings.TotpFailureWindowMinutes, cancellationToken);
        if (count >= settings.TotpFailureLimit)
        {
            await _audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.TotpCooldown,
                Outcome = Outcomes.Denied,
                Severity = Severities.High,
                ActorUserId = userId,
                TargetUserId = userId,
                Details = "Authenticator attempts reached the cooldown threshold."
            }, cancellationToken);
        }
    }
}
