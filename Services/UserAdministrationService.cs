using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;

namespace SecureGate1.Services;

public class UserAdministrationService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly SecurityPolicyOptions _policy;
    private readonly TimeProvider _time;

    public UserAdministrationService(
        UserManager<ApplicationUser> users,
        ApplicationDbContext db,
        AuditService audit,
        Microsoft.Extensions.Options.IOptions<SecurityPolicyOptions> policy,
        TimeProvider time)
    {
        _users = users;
        _db = db;
        _audit = audit;
        _policy = policy.Value;
        _time = time;
    }

    public async Task<CreateUserResult> CreateAsync(
        string actorId,
        string email,
        string displayName,
        string role,
        string? confirmation,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAccountAdminAsync(actorId))
            return new CreateUserResult { Message = "Only an administrator can create accounts." };

        var emailError = AccountRules.Email(email);
        var nameError = AccountRules.DisplayName(displayName);
        if (emailError is not null || nameError is not null)
            return new CreateUserResult { Message = emailError ?? nameError! };
        if (!Roles.IsAssignable(role))
            return new CreateUserResult { Message = "Choose Administrator, Security Officer, or Employee." };
        if (role == Roles.Administrator && confirmation != "CREATE ADMINISTRATOR")
            return new CreateUserResult { Message = "Type CREATE ADMINISTRATOR to confirm an administrator account." };

        var normalizedEmail = email.Trim();
        var tempPassword = GenerateTemporaryPassword();
        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            DisplayName = displayName.Trim(),
            EmailConfirmed = true,
            MustChangePassword = true,
            LockoutEnabled = true,
            IsActive = true,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
            CreatedById = actorId
        };
        var created = await _users.CreateAsync(user, tempPassword);
        if (!created.Succeeded)
        {
            var duplicate = created.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail");
            return new CreateUserResult
            {
                Message = duplicate
                    ? "That email is already in use."
                    : "The account could not be created. Check the details and try again."
            };
        }

        var roleResult = await _users.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await _users.DeleteAsync(user);
            return new CreateUserResult { Message = "The account could not be assigned a role." };
        }

        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.AccountCreated,
            Outcome = Outcomes.Success,
            Severity = role == Roles.Administrator ? Severities.Critical : Severities.Info,
            ActorUserId = actorId,
            TargetUserId = user.Id,
            SubjectIdentifier = user.Email ?? user.Id,
            Details = $"Role={role}"
        }, cancellationToken);

        return new CreateUserResult
        {
            Succeeded = true,
            UserId = user.Id,
            TemporaryPassword = tempPassword,
            Message = "Account created. Give the temporary password to the employee through a trusted channel. It will not be shown again."
        };
    }

    public async Task<ServiceResult> ChangeRoleAsync(string actorId, string targetId, string newRole, string? confirmation, CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAccountAdminAsync(actorId))
            return ServiceResult.Fail("Only an administrator can change roles.");
        if (actorId == targetId)
            return ServiceResult.Fail("You cannot change your own role.");
        if (!Roles.IsAssignable(newRole))
            return ServiceResult.Fail("Choose Administrator, Security Officer, or Employee.");
        if (confirmation != "CHANGE ROLE")
            return ServiceResult.Fail("Type CHANGE ROLE to confirm.");

        var target = await _users.FindByIdAsync(targetId);
        if (target is null)
            return ServiceResult.Fail("That account was not found.");
        if (await _users.IsInRoleAsync(target, Roles.Superadmin))
            return ServiceResult.Fail("The Superadmin role cannot be changed through account management.");
        if (await IsLastActiveAdministratorAsync(target) && newRole != Roles.Administrator)
            return ServiceResult.Fail("The last active administrator cannot be given another role.");

        if (!await _users.IsInRoleAsync(target, newRole))
        {
            var added = await _users.AddToRoleAsync(target, newRole);
            if (!added.Succeeded)
                return ServiceResult.Fail("The role could not be changed.");
        }

        var others = (await _users.GetRolesAsync(target)).Where(r => r != newRole).ToList();
        if (others.Count > 0)
            await _users.RemoveFromRolesAsync(target, others);
        await _users.UpdateSecurityStampAsync(target);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.RoleChanged,
            Outcome = Outcomes.Success,
            Severity = Severities.High,
            ActorUserId = actorId,
            TargetUserId = target.Id,
            SubjectIdentifier = target.Email ?? target.Id,
            Details = $"Role={newRole}"
        }, cancellationToken);
        return ServiceResult.Ok("Role updated. Existing sessions for that account were signed out.");
    }

    public async Task<ServiceResult> SetActiveAsync(string actorId, string targetId, bool active, string? confirmation, CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAccountAdminAsync(actorId))
            return ServiceResult.Fail("Only an administrator can change account status.");
        if (actorId == targetId)
            return ServiceResult.Fail("You cannot change your own account status.");
        var expected = active ? "REACTIVATE" : "DISABLE";
        if (confirmation != expected)
            return ServiceResult.Fail($"Type {expected} to confirm.");

        var target = await _users.FindByIdAsync(targetId);
        if (target is null)
            return ServiceResult.Fail("That account was not found.");
        if (await _users.IsInRoleAsync(target, Roles.Superadmin))
            return ServiceResult.Fail("The Superadmin account cannot be disabled through account management.");
        if (!active && await IsLastActiveAdministratorAsync(target))
            return ServiceResult.Fail("The last active administrator cannot be disabled.");

        target.IsActive = active;
        target.DeactivatedAtUtc = active ? null : _time.GetUtcNow().UtcDateTime;
        var updated = await _users.UpdateAsync(target);
        if (!updated.Succeeded)
            return ServiceResult.Fail("The account status could not be saved.");
        await _users.UpdateSecurityStampAsync(target);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = active ? EventTypes.AccountReactivated : EventTypes.AccountDisabled,
            Outcome = Outcomes.Success,
            Severity = active ? Severities.Info : Severities.High,
            ActorUserId = actorId,
            TargetUserId = target.Id,
            SubjectIdentifier = target.Email ?? target.Id,
            Details = active ? "Account reactivated." : "Account disabled."
        }, cancellationToken);
        return ServiceResult.Ok(active ? "Account reactivated." : "Account disabled and signed out.");
    }

    public async Task<TemporaryPasswordResult> ResetPasswordAsync(string actorId, string targetId, CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAccountAdminAsync(actorId))
            return new TemporaryPasswordResult { Message = "Only an administrator can reset a password." };

        if (actorId == targetId)
        {
            var actor = await _users.FindByIdAsync(actorId);
            if (actor is not null && await _users.IsInRoleAsync(actor, Roles.Superadmin))
                return new TemporaryPasswordResult { Message = "You cannot set a temporary password for your own account." };
        }

        var target = await _users.FindByIdAsync(targetId);
        if (target is null)
            return new TemporaryPasswordResult { Message = "That account was not found." };
        if (await _users.IsInRoleAsync(target, Roles.Superadmin))
            return new TemporaryPasswordResult { Message = "The Superadmin account password cannot be reset here." };

        var tempPassword = GenerateTemporaryPassword();
        var token = await _users.GeneratePasswordResetTokenAsync(target);
        var reset = await _users.ResetPasswordAsync(target, token, tempPassword);
        if (!reset.Succeeded)
            return new TemporaryPasswordResult { Message = "The password could not be reset." };

        target.MustChangePassword = true;
        await _users.UpdateAsync(target);
        await _users.UpdateSecurityStampAsync(target);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.PasswordResetByAdmin,
            Outcome = Outcomes.Success,
            Severity = Severities.High,
            ActorUserId = actorId,
            TargetUserId = target.Id,
            SubjectIdentifier = target.Email ?? target.Id,
            Details = "An administrator set a new temporary password."
        }, cancellationToken);
        return new TemporaryPasswordResult
        {
            Succeeded = true,
            TemporaryPassword = tempPassword,
            Message = "Temporary password created. It will not be shown again."
        };
    }

    public async Task<IReadOnlyList<UserListItem>> ListAsync(
        string? search,
        string? role,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = FilteredUsers(search, role, status);
        return await query
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListItem
            {
                Id = u.Id,
                Email = u.Email ?? "",
                DisplayName = u.DisplayName,
                ProfilePhotoPath = u.ProfilePhotoPath,
                IsActive = u.IsActive,
                MustChangePassword = u.MustChangePassword,
                CreatedAtUtc = u.CreatedAtUtc,
                LastSignInAtUtc = u.LastSignInAtUtc,
                DeactivatedAtUtc = u.DeactivatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(string? search, string? role, string? status, CancellationToken cancellationToken = default) =>
        FilteredUsers(search, role, status).CountAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, string>> RolesForAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToList();
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

    public async Task<string?> RoleNameAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null)
            return null;
        var roles = await _users.GetRolesAsync(user);
        return roles.FirstOrDefault();
    }

    private IQueryable<ApplicationUser> FilteredUsers(string? search, string? role, string? status)
    {
        var query = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                (u.Email != null && u.Email.Contains(term)) ||
                u.DisplayName.Contains(term));
        }

        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.IsActive);
        else if (string.Equals(status, "disabled", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => !u.IsActive);

        if (!string.IsNullOrWhiteSpace(role) && Roles.All.Contains(role))
        {
            query = query.Where(u =>
                _db.UserRoles.Any(ur => ur.UserId == u.Id &&
                    _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));
        }

        return query;
    }

    private async Task<bool> IsActiveAccountAdminAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is not { IsActive: true })
            return false;
        return await _users.IsInRoleAsync(user, Roles.Superadmin)
            || await _users.IsInRoleAsync(user, Roles.Administrator);
    }

    private async Task<bool> IsLastActiveAdministratorAsync(ApplicationUser user)
    {
        if (!user.IsActive || !await _users.IsInRoleAsync(user, Roles.Administrator))
            return false;
        var admins = await _users.GetUsersInRoleAsync(Roles.Administrator);
        return admins.Count(a => a.IsActive) <= 1;
    }

    public static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        return RandomNumberGenerator.GetString(alphabet, 20);
    }
}

public sealed class UserListItem
{
    public string Id { get; init; } = "";
    public string Email { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? ProfilePhotoPath { get; init; }
    public bool IsActive { get; init; }
    public bool MustChangePassword { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? LastSignInAtUtc { get; init; }
    public DateTime? DeactivatedAtUtc { get; init; }
}
