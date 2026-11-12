using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;

namespace SecureGate1.Services;

public class SettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;
    private readonly TimeProvider _time;

    public SettingsService(ApplicationDbContext db, UserManager<ApplicationUser> users, AuditService audit, TimeProvider time)
    {
        _db = db;
        _users = users;
        _audit = audit;
        _time = time;
    }

    public async Task<SecuritySettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.SecuritySettings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);
        if (settings is null)
            throw new InvalidOperationException("Security settings have not been initialized. Apply the database migrations.");
        return settings;
    }

    public async Task<ServiceResult> UpdateAsync(SecuritySettings posted, byte[] rowVersion, string actorId, CancellationToken cancellationToken = default)
    {
        if (!await IsAdministratorAsync(actorId, cancellationToken))
            return ServiceResult.Fail("Only an administrator can change security settings.");

        var errors = SecuritySettingsValidator.Validate(posted);
        if (errors.Count > 0)
            return ServiceResult.Fail(string.Join(" ", errors));

        var current = await GetAsync(cancellationToken);
        _db.Entry(current).Property(s => s.RowVersion).OriginalValue = rowVersion;
        current.LockoutFailedAttempts = posted.LockoutFailedAttempts;
        current.LockoutMinutes = posted.LockoutMinutes;
        current.TotpFailureLimit = posted.TotpFailureLimit;
        current.TotpFailureWindowMinutes = posted.TotpFailureWindowMinutes;
        current.TotpCooldownMinutes = posted.TotpCooldownMinutes;
        current.RecoveryFailureLimit = posted.RecoveryFailureLimit;
        current.RecoveryFailureWindowMinutes = posted.RecoveryFailureWindowMinutes;
        current.RecoveryCooldownMinutes = posted.RecoveryCooldownMinutes;
        current.StepUpMinutes = posted.StepUpMinutes;
        current.AuditRetentionDays = posted.AuditRetentionDays;
        current.UpdatedById = actorId;
        current.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("These settings were changed by someone else. Reload the page and try again.");
        }

        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.SecuritySettingsChanged,
            Outcome = Outcomes.Success,
            Severity = Severities.High,
            ActorUserId = actorId,
            TargetUserId = actorId,
            Details = $"Lockout={current.LockoutFailedAttempts}/{current.LockoutMinutes}m; Totp={current.TotpFailureLimit}/{current.TotpFailureWindowMinutes}m/{current.TotpCooldownMinutes}m; Recovery={current.RecoveryFailureLimit}/{current.RecoveryFailureWindowMinutes}m/{current.RecoveryCooldownMinutes}m; StepUp={current.StepUpMinutes}m; Retention={current.AuditRetentionDays}d"
        }, cancellationToken);
        return ServiceResult.Ok("Security settings saved.");
    }

    private async Task<bool> IsAdministratorAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(userId);
        return user is { IsActive: true }
            && (await _users.IsInRoleAsync(user, Roles.Superadmin)
                || await _users.IsInRoleAsync(user, Roles.Administrator));
    }
}
