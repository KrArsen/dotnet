using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Activities.Application.Caching;

/// <summary>
/// Дворівневий кеш (L1 IMemoryCache -> L2 Redis IDistributedCache -> MongoDB) для сервісу Reviews/Activities.
/// </summary>
public sealed class ReviewsTwoLevelCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMemoryCache _memory;
    private readonly IDistributedCache? _redis;
    private readonly ReviewsCacheMetrics _metrics;
    private readonly ILogger<ReviewsTwoLevelCacheService> _logger;

    public ReviewsTwoLevelCacheService(
        IMemoryCache memory,
        ReviewsCacheMetrics metrics,
        ILogger<ReviewsTwoLevelCacheService> logger,
        IDistributedCache? redis = null)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redis = redis;
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan l1Ttl,
        TimeSpan l2Ttl,
        TimeSpan? l1Sliding = null,
        long size = 1,
        CancellationToken ct = default)
    {
        var sliding = l1Sliding ?? TimeSpan.FromTicks(Math.Max(l1Ttl.Ticks / 2, TimeSpan.FromSeconds(5).Ticks));

        if (_memory.TryGetValue(key, out T? value) && value is not null)
        {
            _metrics.Hit("L1");
            _logger.LogInformation("Cache {Outcome} ({Level}) for {Key}", "HIT", "L1", key);
            return value;
        }

        _metrics.Miss("L1");

        if (_redis is not null)
        {
            try
            {
                var bytes = await _redis.GetAsync(key, ct);
                if (bytes is { Length: > 0 })
                {
                    _metrics.Hit("L2");
                    _logger.LogInformation("Cache {Outcome} ({Level}) for {Key}", "HIT", "L2", key);

                    var fromRedis = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
                    if (fromRedis is not null)
                    {
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
                _logger.LogWarning(ex, "Redis L2 read failed for {Key}; falling back to MongoDB", key);
            }
        }

        _metrics.Miss("L2");
        _logger.LogInformation("Cache {Outcome} ({Level}) for {Key} — querying MongoDB", "MISS", "L2", key);

        var fresh = await factory();
        if (fresh is null)
        {
            return default;
        }

        if (_redis is not null)
        {
            try
            {
                var serialized = JsonSerializer.SerializeToUtf8Bytes(fresh, JsonOptions);
                await _redis.SetAsync(
                    key,
                    serialized,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = l2Ttl },
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis L2 write failed for {Key}", key);
            }
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
        _memory.Remove(key);

        if (_redis is not null)
        {
            try
            {
                await _redis.RemoveAsync(key, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis L2 invalidation failed for {Key}", key);
            }
        }

        _metrics.Invalidated(reason);
        _logger.LogInformation("Cache invalidated {Key}, reason {Reason}", key, reason);
    }
}
