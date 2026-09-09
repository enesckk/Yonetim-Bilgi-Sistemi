using System.Collections.Concurrent;

namespace PersonelYonetim.Application.Common.Security;

/// <summary>
/// IP başına kayan pencere ile giriş denemesi sınırı (bellek içi).
/// </summary>
public sealed class LoginAttemptGate
{
    private readonly int _max;
    private readonly TimeSpan _window;
    private readonly Func<DateTime> _utcNow;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _hits = new();

    public LoginAttemptGate(int maxAttempts = 20, TimeSpan? window = null, Func<DateTime>? utcNow = null)
    {
        _max = Math.Max(1, maxAttempts);
        _window = window ?? TimeSpan.FromMinutes(15);
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public bool TryRecord(string? key)
    {
        var id = string.IsNullOrWhiteSpace(key) ? "unknown" : key.Trim();
        var now = _utcNow();
        var cutoff = now - _window;
        var queue = _hits.GetOrAdd(id, _ => new ConcurrentQueue<DateTime>());
        queue.Enqueue(now);
        while (queue.TryPeek(out var oldest) && oldest < cutoff)
            queue.TryDequeue(out _);
        return queue.Count <= _max;
    }
}
