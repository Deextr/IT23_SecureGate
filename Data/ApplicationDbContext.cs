using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureGate1.Models;

namespace SecureGate1.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserMfaSetting> UserMfaSettings => Set<UserMfaSetting>();
    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();
    public DbSet<AuthenticationAuditLog> AuthenticationAuditLogs => Set<AuthenticationAuditLog>();
    public DbSet<SecurityAlert> SecurityAlerts => Set<SecurityAlert>();
    public DbSet<InvestigationRecord> InvestigationRecords => Set<InvestigationRecord>();
    public DbSet<SecuritySettings> SecuritySettings => Set<SecuritySettings>();
    public DbSet<MfaResetRequest> MfaResetRequests => Set<MfaResetRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.ProfilePhotoPath).HasMaxLength(260);
            entity.Property(u => u.CreatedById).HasMaxLength(450);
            entity.Property(u => u.CreatedAtUtc).HasColumnType("datetime2");
            entity.Property(u => u.DeactivatedAtUtc).HasColumnType("datetime2");
            entity.Property(u => u.LastSignInAtUtc).HasColumnType("datetime2");
        });

        builder.Entity<UserMfaSetting>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.ProtectedSecret).HasMaxLength(1000);
            entity.Property(e => e.EnrolledAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.LastVerifiedAtUtc).HasColumnType("datetime2");
            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<UserMfaSetting>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserRecoveryCode>(entity =>
        {
            entity.HasKey(e => e.RecoveryCodeId);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.Salt).HasMaxLength(16).IsRequired();
            entity.Property(e => e.CodeHash).HasMaxLength(32).IsRequired();
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.UsedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.RevokedAtUtc).HasColumnType("datetime2");
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.UserId, e.UsedAtUtc, e.RevokedAtUtc });
        });

        builder.Entity<AuthenticationAuditLog>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).UseIdentityColumn();
            entity.Property(e => e.OccurredAtUtc).HasColumnType("datetime2").IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Outcome).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Severity).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ActorUserId).HasMaxLength(450);
            entity.Property(e => e.TargetUserId).HasMaxLength(450);
            entity.Property(e => e.SubjectIdentifier).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(512);
            entity.Property(e => e.CorrelationId).HasMaxLength(64);
            entity.Property(e => e.Details).HasMaxLength(1000);
            entity.HasIndex(e => e.OccurredAtUtc);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Outcome);
            entity.HasIndex(e => e.ActorUserId);
            entity.HasIndex(e => e.TargetUserId);
            entity.ToTable(t => t.HasTrigger("TR_AuthenticationAuditLogs_AppendOnly"));
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SecurityAlert>(entity =>
        {
            entity.HasKey(e => e.AlertId);
            entity.Property(e => e.RuleKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Severity).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.TargetUserId).HasMaxLength(450);
            entity.Property(e => e.SourceIp).HasMaxLength(45);
            entity.Property(e => e.AcknowledgedById).HasMaxLength(450);
            entity.Property(e => e.ResolvedById).HasMaxLength(450);
            entity.Property(e => e.ResponsibleOfficerId).HasMaxLength(450);
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.AcknowledgedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ResolvedAtUtc).HasColumnType("datetime2");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_SecurityAlerts_Severity", "[Severity] IN ('Low','Medium','High','Critical')");
                t.HasCheckConstraint("CK_SecurityAlerts_Status", "[Status] IN ('Open','Acknowledged','Resolved')");
            });
            entity.HasIndex(e => new { e.RuleKey, e.RelatedEventId })
                .IsUnique()
                .HasFilter("[RelatedEventId] IS NOT NULL");
            entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
            entity.HasOne(e => e.RelatedEvent)
                .WithMany()
                .HasForeignKey(e => e.RelatedEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvestigationRecord>(entity =>
        {
            entity.HasKey(e => e.InvestigationId);
            entity.Property(e => e.AssignedOfficerId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ClosedAtUtc).HasColumnType("datetime2");
            entity.HasIndex(e => e.AlertId).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_InvestigationRecords_Status",
                "[Status] IN ('Open','InProgress','Closed')"));
            entity.HasOne(e => e.Alert)
                .WithOne(a => a.Investigation)
                .HasForeignKey<InvestigationRecord>(e => e.AlertId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.AssignedOfficerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SecuritySettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UpdatedById).HasMaxLength(450);
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_SecuritySettings_Singleton", "[Id] = 1");
                t.HasCheckConstraint("CK_SecuritySettings_LockoutAttempts", "[LockoutFailedAttempts] BETWEEN 3 AND 20");
                t.HasCheckConstraint("CK_SecuritySettings_LockoutMinutes", "[LockoutMinutes] BETWEEN 1 AND 1440");
                t.HasCheckConstraint("CK_SecuritySettings_TotpLimit", "[TotpFailureLimit] BETWEEN 3 AND 20");
                t.HasCheckConstraint("CK_SecuritySettings_TotpWindow", "[TotpFailureWindowMinutes] BETWEEN 1 AND 60");
                t.HasCheckConstraint("CK_SecuritySettings_TotpCooldown", "[TotpCooldownMinutes] BETWEEN 1 AND 1440");
                t.HasCheckConstraint("CK_SecuritySettings_RecoveryLimit", "[RecoveryFailureLimit] BETWEEN 3 AND 20");
                t.HasCheckConstraint("CK_SecuritySettings_RecoveryWindow", "[RecoveryFailureWindowMinutes] BETWEEN 1 AND 60");
                t.HasCheckConstraint("CK_SecuritySettings_RecoveryCooldown", "[RecoveryCooldownMinutes] BETWEEN 1 AND 1440");
                t.HasCheckConstraint("CK_SecuritySettings_StepUp", "[StepUpMinutes] BETWEEN 1 AND 30");
                t.HasCheckConstraint("CK_SecuritySettings_Retention", "[AuditRetentionDays] BETWEEN 365 AND 3650");
            });
            entity.HasData(new SecuritySettings
            {
                Id = 1,
                LockoutFailedAttempts = 5,
                LockoutMinutes = 15,
                TotpFailureLimit = 5,
                TotpFailureWindowMinutes = 5,
                TotpCooldownMinutes = 15,
                RecoveryFailureLimit = 5,
                RecoveryFailureWindowMinutes = 15,
                RecoveryCooldownMinutes = 15,
                StepUpMinutes = 5,
                AuditRetentionDays = 365,
                UpdatedAtUtc = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        builder.Entity<MfaResetRequest>(entity =>
        {
            entity.HasKey(e => e.MfaResetRequestId);
            entity.Property(e => e.TargetUserId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.RequesterUserId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.VerificationMethod).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ReviewerUserId).HasMaxLength(450);
            entity.Property(e => e.DecisionNotes).HasMaxLength(2000);
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ReviewedAtUtc).HasColumnType("datetime2");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_MfaResetRequests_Status",
                    "[Status] IN ('Pending','Approved','Rejected')");
                t.HasCheckConstraint(
                    "CK_MfaResetRequests_Method",
                    "[VerificationMethod] IN ('InPerson','OrganizationRecord','ManagerConfirmation')");
            });
            entity.HasIndex(e => e.TargetUserId)
                .IsUnique()
                .HasFilter("[Status] = 'Pending'");
            entity.HasOne(e => e.TargetUser)
                .WithMany()
                .HasForeignKey(e => e.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RequesterUser)
                .WithMany()
                .HasForeignKey(e => e.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.ReviewerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
