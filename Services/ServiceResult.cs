namespace SecureGate1.Services;

public class ServiceResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = "";
    public bool RequiresAuthenticatorReset { get; init; }

    public static ServiceResult Ok(string message = "") => new() { Succeeded = true, Message = message };
    public static ServiceResult Fail(string message) => new() { Succeeded = false, Message = message };
    public static ServiceResult FailReset(string message) =>
        new() { Succeeded = false, Message = message, RequiresAuthenticatorReset = true };
}

public sealed class CreateUserResult : ServiceResult
{
    public string? UserId { get; init; }
    public string? TemporaryPassword { get; init; }
}

public sealed class TemporaryPasswordResult : ServiceResult
{
    public string? TemporaryPassword { get; init; }
}

public sealed class PasswordSignInResult
{
    public PasswordStage Stage { get; init; }
    public string Message { get; init; } = "";
    public string? UserId { get; init; }
}

public enum PasswordStage
{
    Failed,
    LockedOut,
    ChangePassword,
    Enroll,
    Verify
}
