using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace BonyadRazi.Portal.Api.Security;

/// <summary>
/// Process-local username login rate limiter.
///
/// SECURITY NOTE:
/// This implementation is safe only for single-instance API deployments.
/// Counters are stored in memory inside the current process. If the API is
/// scaled out to multiple instances, each instance will maintain its own
/// independent counters and the effective login attempt limit will increase
/// roughly by the number of instances.
///
/// Before running more than one API instance, replace this implementation with
/// a distributed implementation backed by Redis, SQL, or another shared store.
/// </summary>
public sealed class InMemoryUsernameLoginRateLimiter : IUsernameLoginRateLimiter
{
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly IOptionsMonitor<UsernameLoginRateLimitOptions> _options;

    private DateTime _lastCleanupUtc = DateTime.MinValue;

    public InMemoryUsernameLoginRateLimiter(
        IOptionsMonitor<UsernameLoginRateLimitOptions> options)
    {
        _options = options;
    }

    public UsernameLoginRateLimitResult Check(string username, DateTime utcNow)
    {
        var options = _options.CurrentValue;

        if (!options.Enabled)
        {
            return UsernameLoginRateLimitResult.Allow();
        }

        var normalizedUsername = NormalizeUsername(username);

        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            // خالی بودن username بعداً در Login validation بررسی می‌شود.
            // اینجا rate limiter نباید validation error را با 429 جایگزین کند.
            return UsernameLoginRateLimitResult.Allow();
        }

        var permitLimit = Math.Max(1, options.PermitLimit);
        var window = TimeSpan.FromMinutes(Math.Max(1, options.WindowMinutes));

        MaybeCleanup(utcNow, options);

        var bucket = _buckets.GetOrAdd(
            normalizedUsername,
            _ => new Bucket
            {
                WindowStartUtc = utcNow,
                LastSeenUtc = utcNow,
                Count = 0
            });

        lock (bucket.Gate)
        {
            bucket.LastSeenUtc = utcNow;

            if (utcNow - bucket.WindowStartUtc >= window)
            {
                bucket.WindowStartUtc = utcNow;
                bucket.Count = 0;
            }

            if (bucket.Count >= permitLimit)
            {
                var retryAtUtc = bucket.WindowStartUtc.Add(window);
                var retryAfter = retryAtUtc - utcNow;

                return UsernameLoginRateLimitResult.Deny(
                    (int)Math.Ceiling(Math.Max(1, retryAfter.TotalSeconds)));
            }

            bucket.Count++;

            return UsernameLoginRateLimitResult.Allow();
        }
    }

    private void MaybeCleanup(DateTime utcNow, UsernameLoginRateLimitOptions options)
    {
        var cleanupInterval = TimeSpan.FromMinutes(Math.Max(1, options.CleanupIntervalMinutes));

        if (utcNow - _lastCleanupUtc < cleanupInterval)
        {
            return;
        }

        _lastCleanupUtc = utcNow;

        var window = TimeSpan.FromMinutes(Math.Max(1, options.WindowMinutes));
        var removeBeforeUtc = utcNow.Subtract(window).Subtract(cleanupInterval);

        foreach (var pair in _buckets)
        {
            var bucket = pair.Value;

            lock (bucket.Gate)
            {
                if (bucket.LastSeenUtc < removeBeforeUtc)
                {
                    _buckets.TryRemove(pair.Key, out _);
                }
            }
        }
    }

    private static string NormalizeUsername(string username)
    {
        return (username ?? string.Empty).Trim().ToUpperInvariant();
    }

    private sealed class Bucket
    {
        public object Gate { get; } = new();

        public DateTime WindowStartUtc { get; set; }

        public DateTime LastSeenUtc { get; set; }

        public int Count { get; set; }
    }
}