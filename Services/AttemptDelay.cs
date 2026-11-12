namespace SecureGate1.Services;

public static class AttemptDelay
{
    public static int Milliseconds(int failureCount) => Math.Min(2000, 200 * Math.Max(1, failureCount));
}

public static class SignInMessages
{
    public const string Generic = "Sign-in was not successful. Check your email and password.";
    public const string Locked = "Sign-in is temporarily unavailable. Try again in a few minutes.";
    public const string MfaFailed = "The verification code was not accepted.";
    public const string RecoveryFailed = "The recovery code was not accepted.";
    public const string Cooldown = "Too many attempts. Try again later.";
    public const string EnrollmentFailed = "The authenticator code was not accepted. Check the code and try again.";
}
