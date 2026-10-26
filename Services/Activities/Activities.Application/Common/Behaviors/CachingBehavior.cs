using Activities.Application.Common.Caching;
using Activities.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Activities.Application.Common.Behaviors;

/// <summary>
/// Pipeline Behavior кешування запитів читання ICacheableQuery з інвалідацією після виконання команд
/// </summary>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IMemoryCache _cache;
    private readonly ICacheVersionProvider _versionProvider;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        IMemoryCache cache,
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
        // 1. Якщо це команда — виконуємо її і при успіху інвалідуємо кеш запитів
        if (request is ICommand || request is ICommand<TResponse>)
        {
            var commandResult = await next();
            _versionProvider.Invalidate();
            _logger.LogDebug("Cache invalidated due to execution of mutation command {RequestName}", typeof(TRequest).Name);
            return commandResult;
        }

        // 2. Якщо це кешований запит — перевіряємо наявність у пам'яті
        if (request is ICacheableQuery cacheable)
        {
            var versionedKey = $"{cacheable.CacheKey}:v{_versionProvider.CurrentVersion}";

            if (_cache.TryGetValue(versionedKey, out TResponse? cachedResponse) && cachedResponse != null)
            {
                _logger.LogDebug("Cache HIT for key: {CacheKey}", versionedKey);
                return cachedResponse;
            }

            _logger.LogDebug("Cache MISS for key: {CacheKey}", versionedKey);
            var freshResponse = await next();

            var expiration = cacheable.Expiration ?? TimeSpan.FromSeconds(30);
            _cache.Set(versionedKey, freshResponse, expiration);

            return freshResponse;
        }

        return await next();
    }
}
