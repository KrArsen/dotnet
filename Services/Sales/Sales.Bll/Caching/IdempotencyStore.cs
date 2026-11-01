using Microsoft.Extensions.Caching.Memory;

namespace Sales.Bll.Caching;

public record IdempotentResponse(int StatusCode, object? Body);

public interface IIdempotencyStore
{
    Task<IdempotentResponse?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, int statusCode, object? body, TimeSpan? ttl = null, CancellationToken ct = default);
}

public class MemoryIdempotencyStore : IIdempotencyStore
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public MemoryIdempotencyStore(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public Task<IdempotentResponse?> GetAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<IdempotentResponse?>(null);
        }

        var fullKey = $"idempotency:{key}";
        if (_cache.TryGetValue(fullKey, out IdempotentResponse? response))
        {
            return Task.FromResult(response);
        }

        return Task.FromResult<IdempotentResponse?>(null);
    }

    public Task SetAsync(string key, int statusCode, object? body, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.CompletedTask;
        }

        var fullKey = $"idempotency:{key}";
        _cache.Set(fullKey, new IdempotentResponse(statusCode, body), new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl
        });
        return Task.CompletedTask;
    }
}
