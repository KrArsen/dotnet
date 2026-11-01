using Catalog.Bll.DTOs;
using Catalog.Bll.Services;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Caching;

/// <summary>
/// Декоратор ICategoryService з дворівневим кешуванням (L1 пам'ять -> L2 Redis -> БД).
/// Категорії читаються часто і змінюються рідко, тому мають довший TTL (L1 = 2 хв, L2 = 30 хв).
/// </summary>
public sealed class CachedCategoryService : ICategoryService
{
    private readonly ICategoryService _inner;
    private readonly ITwoLevelCacheService _cache;
    private readonly ILogger<CachedCategoryService> _logger;

    private static readonly TimeSpan CategoryL1Ttl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CategoryL1Sliding = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CategoryL2Ttl = TimeSpan.FromMinutes(30);

    public const string AllCategoriesKey = "catalog:categories:all";
    public static string CategoryByIdKey(long id) => $"catalog:category:{id}";
    public static string CategoryWithProductsKey(long id) => $"catalog:category:{id}:with-products";

    public CachedCategoryService(
        ICategoryService inner,
        ITwoLevelCacheService cache,
        ILogger<CachedCategoryService> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var result = await _cache.GetOrSetAsync(
            AllCategoriesKey,
            () => _inner.GetAllAsync(ct)!,
            l1Ttl: CategoryL1Ttl,
            l2Ttl: CategoryL2Ttl,
            l1Sliding: CategoryL1Sliding,
            size: 2,
            ct: ct);

        return result!;
    }

    public async Task<CategoryDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var key = CategoryByIdKey(id);

        var result = await _cache.GetOrSetAsync(
            key,
            () => _inner.GetByIdAsync(id, ct)!,
            l1Ttl: CategoryL1Ttl,
            l2Ttl: CategoryL2Ttl,
            l1Sliding: CategoryL1Sliding,
            size: 1,
            ct: ct);

        return result!;
    }

    public async Task<CategoryWithProductsDto> GetWithProductsExplicitAsync(long id, CancellationToken ct = default)
    {
        var key = CategoryWithProductsKey(id);

        var result = await _cache.GetOrSetAsync(
            key,
            () => _inner.GetWithProductsExplicitAsync(id, ct)!,
            l1Ttl: TimeSpan.FromMinutes(1),
            l2Ttl: TimeSpan.FromMinutes(10),
            l1Sliding: TimeSpan.FromSeconds(30),
            size: 3,
            ct: ct);

        return result!;
    }

    public Task<IReadOnlyList<ProductDto>> GetRecentlyAddedProductsAsync(string categoryName, DateTime since, CancellationToken ct = default)
    {
        return _inner.GetRecentlyAddedProductsAsync(categoryName, since, ct);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, string createdBy = "system", CancellationToken ct = default)
    {
        var created = await _inner.CreateAsync(dto, createdBy, ct);
        await _cache.RemoveAsync(AllCategoriesKey, "CategoryCreated", ct);
        _logger.LogInformation("Cache invalidated {Key}, reason {Reason}", AllCategoriesKey, "CategoryCreated");
        return created;
    }
}
