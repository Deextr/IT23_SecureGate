using System.Text;
using SecureGate1.Models;

namespace SecureGate1.Services;

public static class RecoveryNotesFormatter
{
    public static ServiceResult? ValidateChecks(IReadOnlyList<string>? checks, string? otherNotes)
    {
        var selected = NormalizeChecks(checks);
        if (selected.Count == 0)
            return ServiceResult.Fail("Select at least one verification check you performed.");

        if (selected.Contains(RecoveryVerificationChecks.Other))
        {
            var other = AuditService.Clean(otherNotes, 500) ?? "";
            if (other.Length < 5)
                return ServiceResult.Fail("Provide a short explanation when you select Other.");
        }

        return null;
    }

    public static string BuildStorageNotes(IReadOnlyList<string>? checks, string? otherNotes)
    {
        var selected = NormalizeChecks(checks);
        var sb = new StringBuilder();
        sb.AppendLine("Verification performed:");
        foreach (var key in selected.Where(k => k != RecoveryVerificationChecks.Other))
            sb.AppendLine($"• {RecoveryVerificationChecks.Label(key)}");
        if (selected.Contains(RecoveryVerificationChecks.Other))
        {
            var other = AuditService.Clean(otherNotes, 500) ?? "";
            sb.AppendLine($"• Other: {other}");
        }

        return sb.ToString().TrimEnd();
    }

    public static IReadOnlyList<string> ParseChecklistItems(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return [];

        var text = notes.Trim();
        const string prefix = "Verification performed:";
        if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            text = text[prefix.Length..].TrimStart();

        if (text.Contains('\n') || text.Contains('\r'))
        {
            return text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(StripBulletPrefix)
                .Where(s => s.Length > 0)
                .ToList();
        }

        return text
            .Split('•', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static string StripBulletPrefix(string line)
    {
        line = line.Trim();
        if (line.StartsWith('•'))
            line = line[1..].TrimStart();
        return line;
    }

    private static List<string> NormalizeChecks(IReadOnlyList<string>? checks) =>
        checks?
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Where(RecoveryVerificationChecks.All.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
}
