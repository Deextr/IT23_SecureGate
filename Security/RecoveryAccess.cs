using System.Security.Claims;
using SecureGate1.Models;

namespace SecureGate1.Security;

public static class RecoveryAccess
{
    public static bool CanSubmit(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Superadmin)
        || user.IsInRole(Roles.Administrator)
        || user.IsInRole(Roles.SecurityOfficer);

    public static bool CanManage(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Superadmin) || user.IsInRole(Roles.Administrator);
}
