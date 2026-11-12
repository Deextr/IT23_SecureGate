using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;

namespace SecureGate1.Services;

public class MfaResetService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MfaService _mfa;
    private readonly RecoveryCodeService _recovery;
    private readonly AuditService _audit;
    private readonly TimeProvider _time;

    public MfaResetService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        MfaService mfa,
        RecoveryCodeService recovery,
        AuditService audit,
        TimeProvider time)
    {
        _db = db;
        _users = users;
        _mfa = mfa;
        _recovery = recovery;
        _audit = audit;
        _time = time;
    }

    public async Task<ServiceResult> SubmitAsync(
        string actorId,
        string targetId,
        string verificationMethod,
        string notes,
        CancellationToken cancellationToken = default)
    {
        var actor = await _users.FindByIdAsync(actorId);
        if (actor is not { IsActive: true })
            return ServiceResult.Fail("Your account cannot submit this request.");
        var isAdmin = await _users.IsInRoleAsync(actor, Roles.Superadmin)
            || await _users.IsInRoleAsync(actor, Roles.Administrator);
        var isOfficer = await _users.IsInRoleAsync(actor, Roles.SecurityOfficer);
        if (!isAdmin && !isOfficer)
            return ServiceResult.Fail("You cannot submit a recovery request.");
        if (!VerificationMethods.All.Contains(verificationMethod))
            return ServiceResult.Fail("Choose how the employee's identity was verified.");

        var cleanNotes = AuditService.Clean(notes, 2000) ?? "";
        if (cleanNotes.Length < 10)
            return ServiceResult.Fail("Describe the identity check in at least 10 characters. Do not rely on the employee's own email.");

        var target = await _users.FindByIdAsync(targetId);
        if (target is not { IsActive: true })
            return ServiceResult.Fail("That employee account was not found or is disabled.");

        var pending = await _db.MfaResetRequests.AnyAsync(
            r => r.TargetUserId == target.Id && r.Status == ResetRequestStatuses.Pending,
            cancellationToken);
        if (pending)
            return ServiceResult.Fail("This account already has a pending recovery request.");

        _db.MfaResetRequests.Add(new MfaResetRequest
        {
            TargetUserId = target.Id,
            RequesterUserId = actor.Id,
            VerificationMethod = verificationMethod,
            Notes = cleanNotes,
            Status = ResetRequestStatuses.Pending,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.MfaResetRequested,
            Outcome = Outcomes.Success,
            ActorUserId = actor.Id,
            TargetUserId = target.Id,
            SubjectIdentifier = target.Email ?? target.Id,
            Details = $"Method={verificationMethod}"
        }, cancellationToken);
        return ServiceResult.Ok("Recovery request submitted. An administrator must approve it before the authenticator is reset.");
    }

    public async Task<ServiceResult> ApproveAsync(string actorId, long requestId, string? confirmation, string? decisionNotes, CancellationToken cancellationToken = default)
    {
        if (confirmation != "APPROVE RESET")
            return ServiceResult.Fail("Type APPROVE RESET to confirm.");

        var actor = await _users.FindByIdAsync(actorId);
        if (actor is not { IsActive: true }
            || (!await _users.IsInRoleAsync(actor, Roles.Superadmin) && !await _users.IsInRoleAsync(actor, Roles.Administrator)))
            return ServiceResult.Fail("Only an administrator can approve a recovery request.");

        var request = await _db.MfaResetRequests.SingleOrDefaultAsync(r => r.MfaResetRequestId == requestId, cancellationToken);
        if (request is null || request.Status != ResetRequestStatuses.Pending)
            return ServiceResult.Fail("That request is no longer pending.");

        var target = await _users.FindByIdAsync(request.TargetUserId);
        if (target is null)
            return ServiceResult.Fail("The employee account no longer exists.");
        if (actor.Id == target.Id)
            return ServiceResult.Fail("You cannot approve a reset of your own authenticator.");

        if (await _users.IsInRoleAsync(target, Roles.Administrator))
        {
            var admins = await _users.GetUsersInRoleAsync(Roles.Administrator);
            var otherActiveAdmin = admins.Any(a => a.IsActive && a.Id != target.Id);
            if (!otherActiveAdmin || !admins.Any(a => a.IsActive && a.Id == actor.Id && a.Id != target.Id))
                return ServiceResult.Fail("A different active administrator must approve a reset for an administrator account.");
        }

        await _mfa.ClearBindingAsync(target.Id, cancellationToken);
        await _recovery.RevokeUnusedAsync(target.Id, actor.Id, cancellationToken);
        await _users.UpdateSecurityStampAsync(target);

        request.Status = ResetRequestStatuses.Approved;
        request.ReviewerUserId = actor.Id;
        request.ReviewedAtUtc = _time.GetUtcNow().UtcDateTime;
        request.DecisionNotes = AuditService.Clean(decisionNotes, 2000);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.MfaResetApproved,
            Outcome = Outcomes.Success,
            Severity = Severities.High,
            ActorUserId = actor.Id,
            TargetUserId = target.Id,
            SubjectIdentifier = target.Email ?? target.Id,
            Details = "Authenticator binding and sessions were revoked. Fresh enrollment is required."
        }, cancellationToken);
        return ServiceResult.Ok("Authenticator reset. The employee must enroll again at the next sign-in.");
    }

    public async Task<ServiceResult> RejectAsync(string actorId, long requestId, string? decisionNotes, CancellationToken cancellationToken = default)
    {
        var actor = await _users.FindByIdAsync(actorId);
        if (actor is not { IsActive: true }
            || (!await _users.IsInRoleAsync(actor, Roles.Superadmin) && !await _users.IsInRoleAsync(actor, Roles.Administrator)))
            return ServiceResult.Fail("Only an administrator can reject a recovery request.");

        var request = await _db.MfaResetRequests.SingleOrDefaultAsync(r => r.MfaResetRequestId == requestId, cancellationToken);
        if (request is null || request.Status != ResetRequestStatuses.Pending)
            return ServiceResult.Fail("That request is no longer pending.");

        var notes = AuditService.Clean(decisionNotes, 2000) ?? "";
        if (notes.Length < 5)
            return ServiceResult.Fail("Add a short reason for the rejection.");

        request.Status = ResetRequestStatuses.Rejected;
        request.ReviewerUserId = actor.Id;
        request.ReviewedAtUtc = _time.GetUtcNow().UtcDateTime;
        request.DecisionNotes = notes;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.MfaResetRejected,
            Outcome = Outcomes.Denied,
            ActorUserId = actor.Id,
            TargetUserId = request.TargetUserId,
            Details = "Recovery request rejected."
        }, cancellationToken);
        return ServiceResult.Ok("Recovery request rejected.");
    }
}
