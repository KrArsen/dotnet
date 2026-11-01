using Catalog.Bll.DTOs;
using Catalog.Bll.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Catalog.Bll.Caching;

/// <summary>
/// Декоратор IProductService з дворівневим кешуванням (L1 IMemoryCache -> L2 Redis IDistributedCache -> БД),
/// передбачуваними ключами (catalog:product:{id}) та синхронною інвалідацією після запису.
/// </summary>
public class CachedProductService : IProductService
{
    private readonly IProductService _inner;
    private readonly ITwoLevelCacheService _cache;
    private readonly ILogger<CachedProductService> _logger;

    // Час життя для картки товару: L1 = 1 хв (sliding 30 с), L2 (Redis) = 10 хв (TTL L1 < TTL L2)
    private static readonly TimeSpan ProductL1Ttl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ProductL1Sliding = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProductL2Ttl = TimeSpan.FromMinutes(10);

    // Час життя для списків товарів: L1 = 20 с (sliding 10 с), L2 (Redis) = 2 хв
    private static readonly TimeSpan ListL1Ttl = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ListL1Sliding = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ListL2Ttl = TimeSpan.FromMinutes(2);

    private static long _listVersion = 1;

    public CachedProductService(
        IProductService inner,
        ITwoLevelCacheService cache,
        ILogger<CachedProductService> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public CachedProductService(
        IProductService inner,
        IMemoryCache memoryCache,
        ILogger<CachedProductService> logger)
        : this(
            inner,
            new TwoLevelCacheService(
                memoryCache,
                new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())),
                new CatalogCacheMetrics(),
                NullLogger<TwoLevelCacheService>.Instance),
            logger)
    {
    }

    public static string ProductKey(long id) => $"catalog:product:{id}";
    public static string CategoryProductsKey(long categoryId) => $"catalog:category:{categoryId}:products";
    public static string ProductsListKey(ProductQuery query) =>
        $"catalog:products:list:v{Interlocked.Read(ref _listVersion)}:p{query.Page}:s{query.PageSize}:q{query.Search}:min{query.MinPrice}:max{query.MaxPrice}:t{query.ItemType}:c{query.CategoryId}:sb{query.SortBy}:sd{query.SortDir}";

    public async Task<PagedResult<ProductDto>> GetPagedAsync(ProductQuery query, CancellationToken ct = default)
    {
        var key = ProductsListKey(query);

        var result = await _cache.GetOrSetAsync(
            key,
            () => _inner.GetPagedAsync(query, ct)!,
            l1Ttl: ListL1Ttl,
            l2Ttl: ListL2Ttl,
            l1Sliding: ListL1Sliding,
            size: 5,
            ct: ct);

        return result!;
    }

    public async Task<ProductDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var key = ProductKey(id);

        var product = await _cache.GetOrSetAsync(
            key,
            () => _inner.GetByIdAsync(id, ct)!,
            l1Ttl: ProductL1Ttl,
            l2Ttl: ProductL2Ttl,
            l1Sliding: ProductL1Sliding,
            size: 1,
            ct: ct);

        return product!;
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, string createdBy = "system", CancellationToken ct = default)
    {
        var result = await _inner.CreateAsync(dto, createdBy, ct);
        await InvalidateAfterWriteAsync(result.Id, dto.CategoryIds, "ProductCreated", ct);
        return result;
    }

    public async Task UpdatePriceAsync(long id, UpdatePriceDto dto, string updatedBy = "system", CancellationToken ct = default)
    {
        await _inner.UpdatePriceAsync(id, dto, updatedBy, ct);
        await InvalidateAfterWriteAsync(id, null, "ProductPriceUpdated", ct);
    }

    public async Task RemoveStockAsync(long id, RemoveStockDto dto, CancellationToken ct = default)
    {
        await _inner.RemoveStockAsync(id, dto, ct);
        await InvalidateAfterWriteAsync(id, null, "ProductStockRemoved", ct);
    }

    public async Task AddStockAsync(long id, AddStockDto dto, CancellationToken ct = default)
    {
        await _inner.AddStockAsync(id, dto, ct);
        await InvalidateAfterWriteAsync(id, null, "ProductStockAdded", ct);
    }

    public async Task DeleteAsync(long id, string deletedBy = "system", CancellationToken ct = default)
    {
        await _inner.DeleteAsync(id, deletedBy, ct);
        await InvalidateAfterWriteAsync(id, null, "ProductDeleted", ct);
    }

    public async Task<IReadOnlyList<ProductDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
    {
        var key = CategoryProductsKey(categoryId);

        var result = await _cache.GetOrSetAsync(
            key,
            () => _inner.GetByCategoryAsync(categoryId, ct)!,
            l1Ttl: ProductL1Ttl,
            l2Ttl: ProductL2Ttl,
            l1Sliding: ProductL1Sliding,
            size: 3,
            ct: ct);

        return result!;
    }

    private async Task InvalidateAfterWriteAsync(
        long productId,
        IEnumerable<long>? categoryIds,
        string reason,
        CancellationToken ct)
    {
        Interlocked.Increment(ref _listVersion);

        var productKey = ProductKey(productId);
        await _cache.RemoveAsync(productKey, reason, ct);
        await _cache.RemoveAsync("catalog:products:list", reason, ct);

        if (categoryIds != null)
        {
            foreach (var categoryId in categoryIds.Distinct())
            {
                await _cache.RemoveAsync(CategoryProductsKey(categoryId), reason, ct);
            }
        }

        _logger.LogInformation(
            "Cache invalidated {Key} and related catalog lists synchronously after write, reason {Reason}",
            productKey,
            reason);
    }
}
