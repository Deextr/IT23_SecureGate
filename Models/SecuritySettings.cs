using System.ComponentModel.DataAnnotations;

namespace SecureGate1.Models;

public class SecuritySettings
{
    public int Id { get; set; } = 1;

    [Range(3, 20)]
    public int LockoutFailedAttempts { get; set; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; set; } = 15;

    [Range(3, 20)]
    public int TotpFailureLimit { get; set; } = 5;

    [Range(1, 60)]
    public int TotpFailureWindowMinutes { get; set; } = 5;

    [Range(1, 1440)]
    public int TotpCooldownMinutes { get; set; } = 15;

    [Range(3, 20)]
    public int RecoveryFailureLimit { get; set; } = 5;

    [Range(1, 60)]
    public int RecoveryFailureWindowMinutes { get; set; } = 15;

    [Range(1, 1440)]
    public int RecoveryCooldownMinutes { get; set; } = 15;

    [Range(1, 30)]
    public int StepUpMinutes { get; set; } = 5;

    [Range(365, 3650)]
    public int AuditRetentionDays { get; set; } = 365;
    public string? UpdatedById { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
