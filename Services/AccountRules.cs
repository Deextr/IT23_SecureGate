using System.ComponentModel.DataAnnotations;
using SecureGate1.Security;

namespace SecureGate1.Services;

public static class AccountRules
{
    public static string? DisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "A display name is required.";
        var name = value.Trim();
        if (name.Length is < 2 or > 100)
            return "Display name must be 2 to 100 characters.";
        return null;
    }

    public static string? Email(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "An email address is required.";
        var email = value.Trim();
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            return "Enter a valid email address.";
        return null;
    }

    public static string? Password(string? password, SecurityPolicyOptions policy)
    {
        if (string.IsNullOrEmpty(password))
            return "A password is required.";
        if (password.Length < policy.PasswordMinLength)
            return $"Use at least {policy.PasswordMinLength} characters.";
        if (password.Length > policy.PasswordMaxLength)
            return $"Use at most {policy.PasswordMaxLength} characters.";
        return null;
    }
}

public static class SessionLifetime
{
    public static bool IsAbsoluteExpired(DateTimeOffset issued, DateTimeOffset now, int absoluteHours) =>
        now - issued > TimeSpan.FromHours(absoluteHours);
}
