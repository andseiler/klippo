using System.Collections.Concurrent;

namespace PiiGateway.Infrastructure.Services;

public class PlaygroundUsageTracker
{
    private const int MaxDailyUses = 3;

    private readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _usage = new();

    public bool TryUse(string ip)
    {
        Cleanup();

        var now = DateTime.UtcNow;
        var resetAt = now.Date.AddDays(1); // midnight UTC

        var entry = _usage.AddOrUpdate(
            ip,
            _ => (1, resetAt),
            (_, existing) =>
            {
                if (now >= existing.ResetAt)
                    return (1, resetAt);
                return (existing.Count + 1, existing.ResetAt);
            });

        return entry.Count <= MaxDailyUses;
    }

    public int RemainingUses(string ip)
    {
        if (!_usage.TryGetValue(ip, out var entry))
            return MaxDailyUses;

        if (DateTime.UtcNow >= entry.ResetAt)
            return MaxDailyUses;

        return Math.Max(0, MaxDailyUses - entry.Count);
    }

    public void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _usage)
        {
            if (now >= kvp.Value.ResetAt)
                _usage.TryRemove(kvp.Key, out _);
        }
    }
}
