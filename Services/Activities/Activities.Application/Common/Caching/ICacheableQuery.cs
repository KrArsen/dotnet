namespace Activities.Application.Common.Caching;

/// <summary>
/// Інтерфейс для запитів читання, результати яких підлягають кешуванню у MemoryCache
/// </summary>
public interface ICacheableQuery
{
    /// <summary>
    /// Унікальний ключ кешування для запиту
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Час життя кешованого запису (TTL)
    /// </summary>
    TimeSpan? Expiration { get; }
}
