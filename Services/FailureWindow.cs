using Microsoft.EntityFrameworkCore;
using SecureGate1.Data;
using SecureGate1.Models;

namespace SecureGate1.Services;

public class FailureWindow
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _time;

    public FailureWindow(ApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<bool> InCooldownAsync(string userId, string cooldownEventType, int cooldownMinutes, CancellationToken cancellationToken = default)
    {
        var since = _time.GetUtcNow().UtcDateTime.AddMinutes(-cooldownMinutes);
        return await _db.AuthenticationAuditLogs.AnyAsync(
            e => e.TargetUserId == userId && e.EventType == cooldownEventType && e.OccurredAtUtc >= since,
            cancellationToken);
    }

    public async Task<int> CountSinceAsync(string userId, string eventType, int windowMinutes, CancellationToken cancellationToken = default)
    {
        var since = _time.GetUtcNow().UtcDateTime.AddMinutes(-windowMinutes);
        return await _db.AuthenticationAuditLogs.CountAsync(
            e => e.TargetUserId == userId && e.EventType == eventType && e.OccurredAtUtc >= since,
            cancellationToken);
    }
}
