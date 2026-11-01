using Activities.Application.Caching;
using Activities.Application.Common.Caching;
using Activities.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Activities.Application.Common.Behaviors;

/// <summary>
/// Pipeline Behavior дворівневого кешування запитів читання ICacheableQuery (L1 пам'ять -> L2 Redis -> MongoDB)
/// із синхронною інвалідацією після виконання команд.
/// </summary>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ReviewsTwoLevelCacheService _cache;
    private readonly ICacheVersionProvider _versionProvider;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        ReviewsTwoLevelCacheService cache,
        ICacheVersionProvider versionProvider,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // 1. Якщо це команда — виконуємо її і синхронно інвалідуємо кеш запитів
        if (request is ICommand || request is ICommand<TResponse>)
        {
            var commandResult = await next();
            var reason = typeof(TRequest).Name;
            _versionProvider.Invalidate();
            await _cache.RemoveAsync($"reviews:v{_versionProvider.CurrentVersion - 1}", reason, cancellationToken);
            _logger.LogInformation("Cache invalidated synchronously after command {Reason}", reason);
            return commandResult;
        }

        // 2. Якщо це кешований запит — перевіряємо L1 (IMemoryCache) -> L2 (Redis) -> MongoDB
        if (request is ICacheableQuery cacheable)
        {
            var versionedKey = $"{cacheable.CacheKey}:v{_versionProvider.CurrentVersion}";
            var l1Ttl = cacheable.Expiration ?? TimeSpan.FromSeconds(30);
            var l2Ttl = TimeSpan.FromTicks(l1Ttl.Ticks * 5); // L1 TTL строго коротший за L2 TTL

            var result = await _cache.GetOrSetAsync(
                versionedKey,
                () => next(),
                l1Ttl: l1Ttl,
                l2Ttl: l2Ttl,
                l1Sliding: TimeSpan.FromSeconds(15),
                size: 1,
                ct: cancellationToken);

            return result!;
        }

        return await next();
    }
}
