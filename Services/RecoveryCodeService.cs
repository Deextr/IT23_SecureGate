using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;

namespace SecureGate1.Services;

public sealed class RecoveryCodeKey
{
    public RecoveryCodeKey(byte[] bytes)
    {
        if (bytes.Length < 32)
            throw new ArgumentException("Recovery code key must be at least 32 bytes.", nameof(bytes));
        Bytes = bytes;
    }

    public byte[] Bytes { get; }
}

public class RecoveryCodeService
{
    private readonly ApplicationDbContext _db;
    private readonly RecoveryCodeKey _key;
    private readonly SecurityPolicyOptions _policy;
    private readonly AuditService _audit;
    private readonly TimeProvider _time;

    public RecoveryCodeService(
        ApplicationDbContext db,
        RecoveryCodeKey key,
        IOptions<SecurityPolicyOptions> policy,
        AuditService audit,
        TimeProvider time)
    {
        _db = db;
        _key = key;
        _policy = policy.Value;
        _audit = audit;
        _time = time;
    }

    public IReadOnlyList<string> CreatePlaintextCodes()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var codes = new List<string>(_policy.RecoveryCodeCount);
        for (var i = 0; i < _policy.RecoveryCodeCount; i++)
        {
            var raw = RandomNumberGenerator.GetString(alphabet, 10);
            codes.Add(string.Concat(raw.AsSpan(0, 5), "-", raw.AsSpan(5, 5)));
        }

        return codes;
    }

    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "";
        var chars = code.Trim().ToUpperInvariant().Where(c => c is not ('-' or ' ')).ToArray();
        return new string(chars);
    }

    public byte[] Hash(string normalizedCode, byte[] salt)
    {
        var codeBytes = Encoding.UTF8.GetBytes(normalizedCode);
        var message = new byte[salt.Length + codeBytes.Length];
        salt.CopyTo(message, 0);
        codeBytes.CopyTo(message, salt.Length);
        return HMACSHA256.HashData(_key.Bytes, message);
    }

    public async Task<IReadOnlyList<string>> GenerateAsync(string userId, string actorId, CancellationToken cancellationToken = default)
    {
        var codes = CreatePlaintextCodes();
        var batch = Guid.NewGuid();
        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var code in codes)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            _db.UserRecoveryCodes.Add(new UserRecoveryCode
            {
                UserId = userId,
                BatchId = batch,
                Salt = salt,
                CodeHash = Hash(Normalize(code), salt),
                CreatedAtUtc = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.RecoveryCodesGenerated,
            Outcome = Outcomes.Success,
            ActorUserId = actorId,
            TargetUserId = userId,
            Details = "A new recovery-code set was created."
        }, cancellationToken);
        return codes;
    }

    public async Task RevokeUnusedAsync(string userId, string actorId, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        await _db.UserRecoveryCodes
            .Where(c => c.UserId == userId && c.UsedAtUtc == null && c.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.RevokedAtUtc, now), cancellationToken);
        await _audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.RecoveryCodesRevoked,
            Outcome = Outcomes.Success,
            ActorUserId = actorId,
            TargetUserId = userId,
            Details = "Unused recovery codes were revoked."
        }, cancellationToken);
    }

    public async Task<bool> RedeemAsync(string userId, string submittedCode, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(submittedCode);
        if (normalized.Length is < 8 or > 32)
            return false;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var unused = await _db.UserRecoveryCodes
            .Where(c => c.UserId == userId && c.UsedAtUtc == null && c.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        long? matchId = null;
        if (unused.Count == 0)
        {
            _ = Hash(normalized, new byte[16]);
        }
        else
        {
            foreach (var candidate in unused)
            {
                var hash = Hash(normalized, candidate.Salt);
                if (CryptographicOperations.FixedTimeEquals(hash, candidate.CodeHash))
                    matchId = candidate.RecoveryCodeId;
            }
        }

        if (matchId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var updated = await _db.UserRecoveryCodes
            .Where(c => c.RecoveryCodeId == matchId && c.UsedAtUtc == null && c.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAtUtc, now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated == 1;
    }

    public Task<int> CountUnusedAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.UserRecoveryCodes.CountAsync(
            c => c.UserId == userId && c.UsedAtUtc == null && c.RevokedAtUtc == null,
            cancellationToken);
}
