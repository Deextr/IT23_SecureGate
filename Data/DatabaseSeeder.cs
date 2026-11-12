using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate1.Data;

public static class DatabaseSeeder
{
    private const string DesignatedSuperadminEmail = "admin@davaotech.local";

  private static readonly (string Email, string DisplayName, string Role, string Password)[] DevelopmentDemoAccounts =
    [
        ("securityofficer1@example.com", "Demo Security Officer 1", Roles.SecurityOfficer, "SecureGate-Demo-SO1!"),
        ("securityofficer2@example.com", "Demo Security Officer 2", Roles.SecurityOfficer, "SecureGate-Demo-SO2!"),
        ("employee1@example.com", "Demo Employee 1", Roles.Employee, "SecureGate-Demo-EM1!"),
        ("employee2@example.com", "Demo Employee 2", Roles.Employee, "SecureGate-Demo-EM2!")
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Superadmin, Roles.Administrator, Roles.SecurityOfficer, Roles.Employee })
        {
            if (await roles.RoleExistsAsync(role))
                continue;
            var created = await roles.CreateAsync(new IdentityRole(role));
            if (!created.Succeeded)
                throw new InvalidOperationException($"Could not create the {role} role.");
        }

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var hasPrivilegedAdmin =
            (await users.GetUsersInRoleAsync(Roles.Administrator)).Count > 0
            || (await users.GetUsersInRoleAsync(Roles.Superadmin)).Count > 0;

        if (!hasPrivilegedAdmin)
        {
            var config = services.GetRequiredService<IConfiguration>();
            var email = config["Bootstrap:AdminEmail"]?.Trim();
            var password = config["Bootstrap:AdminTemporaryPassword"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "No administrator exists. Set Bootstrap:AdminEmail and Bootstrap:AdminTemporaryPassword with dotnet user-secrets, then start again.");
            }

            if (password.Length is < 12 or > 128)
                throw new InvalidOperationException("Bootstrap:AdminTemporaryPassword must be 12 to 128 characters.");

            var time = services.GetRequiredService<TimeProvider>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Bootstrap Administrator",
                EmailConfirmed = true,
                MustChangePassword = true,
                LockoutEnabled = true,
                IsActive = true,
                CreatedAtUtc = time.GetUtcNow().UtcDateTime
            };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "The bootstrap administrator could not be created: " + string.Join(" ", result.Errors.Select(e => e.Description)));
            }

            var bootstrapRole = string.Equals(email, DesignatedSuperadminEmail, StringComparison.OrdinalIgnoreCase)
                ? Roles.Superadmin
                : Roles.Administrator;
            var roleResult = await users.AddToRoleAsync(user, bootstrapRole);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"The bootstrap account could not be assigned the {bootstrapRole} role.");

            var audit = services.GetRequiredService<AuditService>();
            await audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.AccountCreated,
                Outcome = Outcomes.Success,
                Severity = Severities.Critical,
                ActorUserId = user.Id,
                TargetUserId = user.Id,
                SubjectIdentifier = user.Email ?? user.Id,
                Details = $"Role={bootstrapRole}. Bootstrap account created."
            });
        }

        await EnsureSingleSuperadminAsync(services, users);

        if (services.GetRequiredService<IHostEnvironment>().IsDevelopment())
            await SeedDevelopmentDemoAccountsAsync(services);
    }

    /// <summary>
    /// Ensures exactly one Superadmin exists at the designated email. Strips Superadmin from all other accounts.
    /// </summary>
    internal static async Task EnsureSingleSuperadminAsync(IServiceProvider services, UserManager<ApplicationUser> users)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var designatedEmail = config["Bootstrap:SuperadminEmail"]?.Trim();
        if (string.IsNullOrWhiteSpace(designatedEmail))
            designatedEmail = DesignatedSuperadminEmail;

        var audit = services.GetRequiredService<AuditService>();
        var existingSuperadmins = await users.GetUsersInRoleAsync(Roles.Superadmin);
        foreach (var account in existingSuperadmins)
        {
            if (string.Equals(account.Email, designatedEmail, StringComparison.OrdinalIgnoreCase))
                continue;
            var removed = await users.RemoveFromRoleAsync(account, Roles.Superadmin);
            if (!removed.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not remove the Superadmin role from {account.Email ?? account.Id}.");
            }

            await audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.RoleChanged,
                Outcome = Outcomes.Success,
                Severity = Severities.Critical,
                ActorUserId = null,
                TargetUserId = account.Id,
                SubjectIdentifier = account.Email ?? account.Id,
                Details = "Role=Superadmin removed. Only one Superadmin is permitted."
            });
        }

        var designated = await users.FindByEmailAsync(designatedEmail);
        if (designated is null)
        {
            throw new InvalidOperationException(
                $"The designated Superadmin account ({designatedEmail}) was not found. Create that account through a controlled process before starting SecureGate.");
        }

        if (!await users.IsInRoleAsync(designated, Roles.Superadmin))
        {
            var added = await users.AddToRoleAsync(designated, Roles.Superadmin);
            if (!added.Succeeded)
                throw new InvalidOperationException($"Could not assign Superadmin to {designatedEmail}.");
        }

        if (await users.IsInRoleAsync(designated, Roles.Administrator))
            await users.RemoveFromRoleAsync(designated, Roles.Administrator);

        var superadminsAfter = await users.GetUsersInRoleAsync(Roles.Superadmin);
        if (superadminsAfter.Count != 1
            || !string.Equals(superadminsAfter[0].Email, designatedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Superadmin reconciliation failed: the system must have exactly one Superadmin.");
        }
    }

    private static async Task SeedDevelopmentDemoAccountsAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var time = services.GetRequiredService<TimeProvider>();
        var audit = services.GetRequiredService<AuditService>();
        var privileged = await users.GetUsersInRoleAsync(Roles.Superadmin);
        var creatorId = privileged.FirstOrDefault()?.Id
            ?? (await users.GetUsersInRoleAsync(Roles.Administrator)).FirstOrDefault()?.Id;

        foreach (var (email, displayName, role, password) in DevelopmentDemoAccounts)
        {
            if (await users.FindByEmailAsync(email) is not null)
                continue;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = displayName,
                EmailConfirmed = true,
                MustChangePassword = false,
                LockoutEnabled = true,
                IsActive = true,
                CreatedAtUtc = time.GetUtcNow().UtcDateTime,
                CreatedById = creatorId
            };

            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed demo account {email}: " + string.Join(" ", created.Errors.Select(e => e.Description)));
            }

            var roleResult = await users.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                await users.DeleteAsync(user);
                throw new InvalidOperationException($"Could not assign {role} to {email}.");
            }

            await audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.AccountCreated,
                Outcome = Outcomes.Success,
                Severity = Severities.Info,
                ActorUserId = creatorId,
                TargetUserId = user.Id,
                SubjectIdentifier = user.Email ?? user.Id,
                Details = $"Role={role}. Development demo account."
            });
        }
    }
}
