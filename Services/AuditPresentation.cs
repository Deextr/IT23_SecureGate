using System.Text.RegularExpressions;
using SecureGate1.Models;
using SecureGate1.Security;

namespace SecureGate1.Services;

public sealed class AuditUserSnapshot
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Email { get; init; }
    public string? RoleKey { get; init; }
    public bool IsActive { get; init; }
}

public sealed class AuditAccountDisplay
{
    public string PrimaryName { get; init; } = "";
    public string? SecondaryLine { get; init; }
    public string? RoleLabel { get; init; }
    public bool IsUnavailable { get; init; }
    public string? UserId { get; init; }

    public static AuditAccountDisplay Unknown { get; } = new() { PrimaryName = "Unknown account" };
}

public sealed class AuditListItem
{
    public AuthenticationAuditLog Event { get; init; } = null!;
    public AuditAccountDisplay Account { get; init; } = AuditAccountDisplay.Unknown;
    public AuditAccountDisplay Actor { get; init; } = AuditAccountDisplay.Unknown;
    public AuditAccountDisplay Target { get; init; } = AuditAccountDisplay.Unknown;
    public string Module { get; init; } = "";
    public string ActionLabel { get; init; } = "";
}

public sealed class AuditEventDetail
{
    public AuthenticationAuditLog Event { get; init; } = null!;
    public AuditAccountDisplay Actor { get; init; } = AuditAccountDisplay.Unknown;
    public AuditAccountDisplay Target { get; init; } = AuditAccountDisplay.Unknown;
    public string Module { get; init; } = "";
    public string ActionLabel { get; init; } = "";
}

public sealed class AuditAccountFilterOption
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Email { get; init; }
}

public static partial class AuditAccountResolver
{
    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    public static AuditAccountDisplay ResolveForList(
        AuthenticationAuditLog entry,
        IReadOnlyDictionary<string, AuditUserSnapshot> users)
    {
        var primaryId = entry.ActorUserId ?? entry.TargetUserId;
        if (primaryId is not null)
            return ResolveUserId(primaryId, entry.SubjectIdentifier, users);

        if (!string.IsNullOrWhiteSpace(entry.SubjectIdentifier))
            return ResolveSubjectOnly(entry.SubjectIdentifier);

        return AuditAccountDisplay.Unknown;
    }

    public static AuditAccountDisplay ResolveUserId(
        string userId,
        string? subjectIdentifier,
        IReadOnlyDictionary<string, AuditUserSnapshot> users)
    {
        if (users.TryGetValue(userId, out var user))
        {
            return new AuditAccountDisplay
            {
                PrimaryName = user.DisplayName,
                SecondaryLine = user.Email,
                RoleLabel = user.RoleKey is null ? null : Roles.DisplayName(user.RoleKey),
                IsUnavailable = !user.IsActive,
                UserId = user.Id
            };
        }

        if (!string.IsNullOrWhiteSpace(subjectIdentifier) && !LooksLikeGuid(subjectIdentifier))
        {
            return new AuditAccountDisplay
            {
                PrimaryName = subjectIdentifier,
                SecondaryLine = LooksLikeEmail(subjectIdentifier) ? subjectIdentifier : "Deleted account",
                IsUnavailable = true,
                UserId = userId
            };
        }

        return new AuditAccountDisplay
        {
            PrimaryName = "Deleted account",
            SecondaryLine = subjectIdentifier,
            IsUnavailable = true,
            UserId = userId
        };
    }

    public static AuditAccountDisplay ResolveSubjectOnly(string subjectIdentifier)
    {
        if (LooksLikeGuid(subjectIdentifier))
        {
            return new AuditAccountDisplay
            {
                PrimaryName = "Unknown account",
                SecondaryLine = null,
                IsUnavailable = true
            };
        }

        return new AuditAccountDisplay
        {
            PrimaryName = subjectIdentifier,
            SecondaryLine = LooksLikeEmail(subjectIdentifier) ? null : null
        };
    }

    public static AuditAccountDisplay ResolveActorOrTarget(
        string? userId,
        string? subjectIdentifier,
        IReadOnlyDictionary<string, AuditUserSnapshot> users)
    {
        if (string.IsNullOrEmpty(userId))
            return string.IsNullOrWhiteSpace(subjectIdentifier)
                ? AuditAccountDisplay.Unknown
                : ResolveSubjectOnly(subjectIdentifier);

        return ResolveUserId(userId, subjectIdentifier, users);
    }

    public static bool LooksLikeGuid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && GuidPattern().IsMatch(value);

    public static bool LooksLikeEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && EmailPattern().IsMatch(value);
}
