namespace SecureGate1.Models;

public class SecuritySettings
{
    public int Id { get; set; } = 1;
    public int LockoutFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int TotpFailureLimit { get; set; } = 5;
    public int TotpFailureWindowMinutes { get; set; } = 5;
    public int TotpCooldownMinutes { get; set; } = 15;
    public int RecoveryFailureLimit { get; set; } = 5;
    public int RecoveryFailureWindowMinutes { get; set; } = 15;
    public int RecoveryCooldownMinutes { get; set; } = 15;
    public int StepUpMinutes { get; set; } = 5;
    public int AuditRetentionDays { get; set; } = 365;
    public string? UpdatedById { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
