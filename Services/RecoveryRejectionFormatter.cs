using SecureGate1.Models;

namespace SecureGate1.Services;

public static class RecoveryRejectionFormatter
{
    public static ServiceResult? Validate(string? reasonKey, string? otherNotes)
    {
        if (string.IsNullOrWhiteSpace(reasonKey) || !RecoveryRejectionReasons.All.Contains(reasonKey))
            return ServiceResult.Fail("Select a rejection reason.");

        if (reasonKey == RecoveryRejectionReasons.Other)
        {
            var other = AuditService.Clean(otherNotes, 500) ?? "";
            if (other.Length < 5)
                return ServiceResult.Fail("Provide a short explanation when you select Other.");
        }

        return null;
    }

    public static string BuildNotes(string reasonKey, string? otherNotes)
    {
        if (reasonKey == RecoveryRejectionReasons.Other)
        {
            var other = AuditService.Clean(otherNotes, 500) ?? "";
            return $"Rejected: {other}";
        }

        return $"Rejected: {RecoveryRejectionReasons.Label(reasonKey)}";
    }
}
