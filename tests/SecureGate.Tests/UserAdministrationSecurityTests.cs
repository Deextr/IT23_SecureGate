using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;
using AppRoles = SecureGate1.Security.Roles;
using SecureGate1.Services;

namespace SecureGate.Tests;

public class UserAdministrationSecurityTests : IDisposable
{
    private readonly ServiceProvider _provider;

    public UserAdministrationSecurityTests()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Options.Create(new SecurityPolicyOptions()));
        services.AddHttpContextAccessor();
        services.AddScoped<AlertService>();
        services.AddScoped<AuditService>();
        services.AddScoped<UserAdministrationService>();

        _provider = services.BuildServiceProvider();
        SeedRolesAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _provider.Dispose();

    private async Task SeedRolesAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private (UserAdministrationService Service, UserManager<ApplicationUser> UserManager) CreateServices()
    {
        var scope = _provider.CreateScope();
        return (
            scope.ServiceProvider.GetRequiredService<UserAdministrationService>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> users, string email, string role, string password = "SecureGate-Test-1!")
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = email,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await users.CreateAsync(user, password);
        await users.AddToRoleAsync(user, role);
        return user;
    }

    [Fact]
    public async Task CreateAsync_RejectsSuperadminRole()
    {
        var (service, userManager) = CreateServices();
        var admin = await CreateUserAsync(userManager, "admin@test.local", AppRoles.Administrator);

        var result = await service.CreateAsync(admin.Id, "new@test.local", "New User", AppRoles.Superadmin, null);
        Assert.False(result.Succeeded);
        Assert.Contains("Administrator", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangeRoleAsync_RejectsSuperadminAssignment()
    {
        var (service, userManager) = CreateServices();
        var superadmin = await CreateUserAsync(userManager, "super@test.local", AppRoles.Superadmin);
        var employee = await CreateUserAsync(userManager, "emp@test.local", AppRoles.Employee);

        var result = await service.ChangeRoleAsync(superadmin.Id, employee.Id, AppRoles.Superadmin, "CHANGE ROLE");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Superadmin_CanIssueTemporaryPassword_ForOtherAccounts()
    {
        var (service, userManager) = CreateServices();
        var superadmin = await CreateUserAsync(userManager, "super@test.local", AppRoles.Superadmin);
        var employee = await CreateUserAsync(userManager, "emp@test.local", AppRoles.Employee);

        var result = await service.ResetPasswordAsync(superadmin.Id, employee.Id);
        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.TemporaryPassword));
    }

    [Fact]
    public async Task Superadmin_CannotIssueTemporaryPassword_ForOwnAccount()
    {
        var (service, userManager) = CreateServices();
        var superadmin = await CreateUserAsync(userManager, "super@test.local", AppRoles.Superadmin);

        var result = await service.ResetPasswordAsync(superadmin.Id, superadmin.Id);
        Assert.False(result.Succeeded);
        Assert.Contains("own", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Administrator_CannotDisableSelf()
    {
        var (service, userManager) = CreateServices();
        var admin = await CreateUserAsync(userManager, "admin@test.local", AppRoles.Administrator);

        var result = await service.SetActiveAsync(admin.Id, admin.Id, active: false, "DISABLE");
        Assert.False(result.Succeeded);
        Assert.Contains("own", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Superadmin_CannotChangeOrDisableSelf()
    {
        var (service, userManager) = CreateServices();
        var superadmin = await CreateUserAsync(userManager, "super@test.local", AppRoles.Superadmin);

        var roleResult = await service.ChangeRoleAsync(superadmin.Id, superadmin.Id, AppRoles.Administrator, "CHANGE ROLE");
        Assert.False(roleResult.Succeeded);

        var statusResult = await service.SetActiveAsync(superadmin.Id, superadmin.Id, active: false, "DISABLE");
        Assert.False(statusResult.Succeeded);
    }

    [Fact]
    public void Roles_IsAssignable_ExcludesSuperadmin()
    {
        Assert.False(AppRoles.IsAssignable(AppRoles.Superadmin));
        Assert.True(AppRoles.IsAssignable(AppRoles.Administrator));
    }
}
