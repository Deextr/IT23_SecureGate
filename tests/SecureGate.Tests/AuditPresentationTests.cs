using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

namespace SecureGate.Tests;

public class AuditPresentationTests
{
    [Fact]
    public void ResolveForList_UsesActorProfile_WhenUserExists()
    {
        var userId = Guid.NewGuid().ToString();
        var users = new Dictionary<string, AuditUserSnapshot>
        {
            [userId] = new()
            {
                Id = userId,
                DisplayName = "Juan Dela Cruz",
                Email = "juan@example.com",
                RoleKey = Roles.Employee,
                IsActive = true
            }
        };
        var entry = new AuthenticationAuditLog
        {
            ActorUserId = userId,
            EventType = EventTypes.TotpSucceeded
        };

        var display = AuditAccountResolver.ResolveForList(entry, users);

        Assert.Equal("Juan Dela Cruz", display.PrimaryName);
        Assert.Equal("juan@example.com", display.SecondaryLine);
        Assert.Equal("Employee", display.RoleLabel);
    }

    [Fact]
    public void ResolveForList_ShowsDeletedAccount_WhenUserMissing()
    {
        var userId = Guid.NewGuid().ToString();
        var entry = new AuthenticationAuditLog
        {
            TargetUserId = userId,
            EventType = EventTypes.Logout
        };

        var display = AuditAccountResolver.ResolveForList(entry, new Dictionary<string, AuditUserSnapshot>());

        Assert.Equal("Deleted account", display.PrimaryName);
        Assert.True(display.IsUnavailable);
        Assert.Equal(userId, display.UserId);
    }

    [Fact]
    public void ResolveForList_UsesSubjectIdentifier_InsteadOfRawGuid()
    {
        var entry = new AuthenticationAuditLog
        {
            SubjectIdentifier = "signer@contoso.com",
            EventType = EventTypes.PasswordFailed
        };

        var display = AuditAccountResolver.ResolveForList(entry, new Dictionary<string, AuditUserSnapshot>());

        Assert.Equal("signer@contoso.com", display.PrimaryName);
    }

    [Fact]
    public void AuditModules_MapsKnownEventTypes()
    {
        Assert.Equal(AuditModules.Mfa, AuditModules.GetModule(EventTypes.TotpFailed));
        Assert.Equal(AuditModules.Authentication, AuditModules.GetModule(EventTypes.PasswordFailed));
        Assert.Contains(EventTypes.TotpFailed, AuditModules.EventTypesForModule(AuditModules.Mfa));
    }
}
