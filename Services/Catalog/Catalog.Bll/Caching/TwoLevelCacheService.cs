using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Caching;

public interface ITwoLevelCacheService
{
    Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T?>> factory,
        TimeSpan l1Ttl,
        TimeSpan l2Ttl,
        TimeSpan? l1Sliding = null,
        long size = 1,
        CancellationToken ct = default);

    Task RemoveAsync(string key, string reason, CancellationToken ct = default);
}

/// <summary>
/// Дворівневий кеш: L1 (IMemoryCache у пам'яті процесу) -> L2 (IDistributedCache у Redis) -> БД.
/// Реалізує патерн Cache-Aside із заповненням рівнів на зворотному шляху та метриками OpenTelemetry.
/// </summary>
public sealed class TwoLevelCacheService : ITwoLevelCacheService
{
    private static readonly ActivitySource CacheActivitySource = new("Catalog.Api");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMemoryCache _memory;
    private readonly IDistributedCache _redis;
    private readonly CatalogCacheMetrics _metrics;
    private readonly ILogger<TwoLevelCacheService> _logger;

    public TwoLevelCacheService(
        IMemoryCache memory,
        IDistributedCache redis,
        CatalogCacheMetrics metrics,
        ILogger<TwoLevelCacheService> logger)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T?>> factory,
        TimeSpan l1Ttl,
        TimeSpan l2Ttl,
        TimeSpan? l1Sliding = null,
        long size = 1,
        CancellationToken ct = default)
    {
        using var activity = CacheActivitySource.StartActivity("Cache.GetOrSet");
        activity?.SetTag("cache.key", key);

        var sliding = l1Sliding ?? TimeSpan.FromTicks(Math.Max(l1Ttl.Ticks / 2, TimeSpan.FromSeconds(5).Ticks));

        // 1. Перевірка рівня L1 (локальна пам'ять процесу)
        if (_memory.TryGetValue(key, out T? value) && value is not null)
        {
            _metrics.Hit("L1");
            activity?.SetTag("cache.level", "L1");
            activity?.SetTag("cache.hit", true);
            _logger.LogInformation("Cache {Outcome} ({Level}) for {Key}", "HIT", "L1", key);
            return value;
        }

        _metrics.Miss("L1");
        _logger.LogDebug("Cache {Outcome} ({Level}) for {Key}", "MISS", "L1", key);

        // 2. Перевірка рівня L2 (розподілений кеш Redis)
        try
        {
            var bytes = await _redis.GetAsync(key, ct);
            if (bytes is { Length: > 0 })
            {
                _metrics.Hit("L2");
                activity?.SetTag("cache.level", "L2");
                activity?.SetTag("cache.hit", true);
                _logger.LogInformation("Cache {Outcome} ({Level}) for {Key}", "HIT", "L2", key);

                var fromRedis = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
                if (fromRedis is not null)
                {
                    // Заповнюємо L1 на зворотному шляху з коротшим TTL ніж L2
                    _memory.Set(key, fromRedis, new MemoryCacheEntryOptions
                    {
                        Size = size,
                        SlidingExpiration = sliding,
                        AbsoluteExpirationRelativeToNow = l1Ttl
                    });
                    return fromRedis;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis L2 cache read failed for {Key}; falling back to database", key);
        }

        _metrics.Miss("L2");
        activity?.SetTag("cache.level", "DB");
        activity?.SetTag("cache.hit", false);
        _logger.LogInformation("Cache {Outcome} ({Level}) for {Key} — querying database", "MISS", "L2", key);

        // 3. Звернення до бази даних та заповнення L2 + L1 на зворотному шляху
        var fresh = await factory();
        if (fresh is null)
        {
            return default;
        }

        try
        {
            var serialized = JsonSerializer.SerializeToUtf8Bytes(fresh, JsonOptions);
            await _redis.SetAsync(
                key,
                serialized,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = l2Ttl
                },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis L2 cache write failed for {Key}", key);
        }

        _memory.Set(key, fresh, new MemoryCacheEntryOptions
        {
            Size = size,
            SlidingExpiration = sliding,
            AbsoluteExpirationRelativeToNow = l1Ttl
        });

        return fresh;
    }

    public async Task RemoveAsync(string key, string reason, CancellationToken ct = default)
    {
        using var activity = CacheActivitySource.StartActivity("Cache.Invalidate");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.reason", reason);

        _memory.Remove(key);

        try
        {
            await _redis.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis L2 cache invalidation failed for {Key}", key);
        }

        _metrics.Invalidated(reason);
        _logger.LogInformation("Cache invalidated {Key}, reason {Reason}", key, reason);
    }
}
