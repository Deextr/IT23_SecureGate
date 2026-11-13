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

    public async Task<IReadOnlyList<MfaResetEmployeeSearchItem>> SearchEmployeesAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return [];

        limit = Math.Clamp(limit, 1, 25);
        var term = query.Trim();

        return await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && (
                u.DisplayName.Contains(term)
                || (u.Email != null && u.Email.Contains(term))
                || (u.UserName != null && u.UserName.Contains(term))
                || u.Id.Contains(term)))
            .OrderBy(u => u.DisplayName)
            .Take(limit)
            .Select(u => new MfaResetEmployeeSearchItem
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Email = u.Email ?? ""
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult> SubmitAsync(
        string actorId,
        string targetId,
        string verificationMethod,
        IReadOnlyList<string> verificationChecks,
        string? otherNotes,
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

        var checkError = RecoveryNotesFormatter.ValidateChecks(verificationChecks, otherNotes);
        if (checkError is not null)
            return checkError;

        var cleanNotes = AuditService.CleanMultiline(RecoveryNotesFormatter.BuildStorageNotes(verificationChecks, otherNotes), 2000) ?? "";
        if (cleanNotes.Length < 10)
            return ServiceResult.Fail("Complete the verification checklist before submitting.");

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

    public async Task<ServiceResult> RejectAsync(
        string actorId,
        long requestId,
        string? rejectionReason,
        string? rejectionOther,
        CancellationToken cancellationToken = default)
    {
        var actor = await _users.FindByIdAsync(actorId);
        if (actor is not { IsActive: true }
            || (!await _users.IsInRoleAsync(actor, Roles.Superadmin) && !await _users.IsInRoleAsync(actor, Roles.Administrator)))
            return ServiceResult.Fail("Only an administrator can reject a recovery request.");

        var request = await _db.MfaResetRequests.SingleOrDefaultAsync(r => r.MfaResetRequestId == requestId, cancellationToken);
        if (request is null || request.Status != ResetRequestStatuses.Pending)
            return ServiceResult.Fail("That request is no longer pending.");

        var validation = RecoveryRejectionFormatter.Validate(rejectionReason, rejectionOther);
        if (validation is not null)
            return validation;

        var reasonKey = rejectionReason!.Trim();
        var notes = AuditService.Clean(RecoveryRejectionFormatter.BuildNotes(reasonKey, rejectionOther), 2000) ?? "";

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

    public Task<int> CountAsync(
        string? search,
        string? status,
        string? targetRole,
        DateTime? submittedFromUtc,
        DateTime? submittedToExclusiveUtc,
        CancellationToken cancellationToken = default) =>
        FilteredRequests(search, status, targetRole, submittedFromUtc, submittedToExclusiveUtc)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<MfaResetListItem>> ListAsync(
        string? search,
        string? status,
        string? targetRole,
        DateTime? submittedFromUtc,
        DateTime? submittedToExclusiveUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var rows = await FilteredRequests(search, status, targetRole, submittedFromUtc, submittedToExclusiveUtc)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.MfaResetRequestId,
                r.CreatedAtUtc,
                r.ReviewedAtUtc,
                r.Status,
                r.VerificationMethod,
                r.Notes,
                r.DecisionNotes,
                r.TargetUserId,
                TargetDisplayName = r.TargetUser.DisplayName,
                TargetEmail = r.TargetUser.Email,
                RequesterUserId = r.RequesterUserId,
                RequesterDisplayName = r.RequesterUser.DisplayName,
                RequesterEmail = r.RequesterUser.Email
            })
            .ToListAsync(cancellationToken);

        var targetIds = rows.Select(r => r.TargetUserId).Distinct().ToList();
        var roles = await RolesForUsersAsync(targetIds, cancellationToken);

        return rows.Select(r => new MfaResetListItem
        {
            MfaResetRequestId = r.MfaResetRequestId,
            CreatedAtUtc = r.CreatedAtUtc,
            ReviewedAtUtc = r.ReviewedAtUtc,
            Status = r.Status,
            VerificationMethod = r.VerificationMethod,
            Notes = r.Notes,
            DecisionNotes = r.DecisionNotes,
            TargetUserId = r.TargetUserId,
            TargetDisplayName = r.TargetDisplayName,
            TargetEmail = r.TargetEmail,
            TargetRole = roles.TryGetValue(r.TargetUserId, out var role) ? role : "",
            RequesterUserId = r.RequesterUserId,
            RequesterDisplayName = r.RequesterDisplayName,
            RequesterEmail = r.RequesterEmail
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<string, string>> RolesForUsersAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<string, string>();

        var pairs = await (
            from userRole in _db.UserRoles
            join role in _db.Roles on userRole.RoleId equals role.Id
            where ids.Contains(userRole.UserId)
            select new { userRole.UserId, role.Name }
        ).ToListAsync(cancellationToken);

        return pairs
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "");
    }

    private IQueryable<MfaResetRequest> FilteredRequests(
        string? search,
        string? status,
        string? targetRole,
        DateTime? submittedFromUtc,
        DateTime? submittedToExclusiveUtc)
    {
        var query = _db.MfaResetRequests
            .AsNoTracking()
            .Include(r => r.TargetUser)
            .Include(r => r.RequesterUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                r.TargetUser.DisplayName.Contains(term)
                || (r.TargetUser.Email != null && r.TargetUser.Email.Contains(term))
                || r.RequesterUser.DisplayName.Contains(term)
                || (r.RequesterUser.Email != null && r.RequesterUser.Email.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (submittedFromUtc.HasValue)
            query = query.Where(r => r.CreatedAtUtc >= submittedFromUtc.Value);

        if (submittedToExclusiveUtc.HasValue)
            query = query.Where(r => r.CreatedAtUtc < submittedToExclusiveUtc.Value);

        if (!string.IsNullOrWhiteSpace(targetRole))
        {
            var roleUserIds = (
                from userRole in _db.UserRoles
                join role in _db.Roles on userRole.RoleId equals role.Id
                where role.Name == targetRole
                select userRole.UserId);
            query = query.Where(r => roleUserIds.Contains(r.TargetUserId));
        }

        return query;
    }
}
