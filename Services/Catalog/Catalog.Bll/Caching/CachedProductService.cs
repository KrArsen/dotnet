using Catalog.Bll.DTOs;
using Catalog.Bll.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Caching;

public class CachedProductService : IProductService
{
    private readonly IProductService _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedProductService> _logger;

    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(5);

    public CachedProductService(
        IProductService inner,
        IMemoryCache cache,
        ILogger<CachedProductService> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<ProductDto>> GetPagedAsync(ProductQuery query, CancellationToken ct = default)
    {
        // Для списків із динамічними параметрами кешування короткоживуче
        var cacheKey = $"catalog_products_{query.Page}_{query.PageSize}_{query.Search}_{query.MinPrice}_{query.MaxPrice}_{query.ItemType}_{query.CategoryId}_{query.SortBy}_{query.SortDir}";

        if (_cache.TryGetValue(cacheKey, out PagedResult<ProductDto>? cached) && cached != null)
        {
            _logger.LogDebug("Cache HIT для списку продуктів: {Key}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Cache MISS для списку продуктів: {Key}", cacheKey);
        var result = await _inner.GetPagedAsync(query, ct);

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(1));
        return result;
    }

    public async Task<ProductDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var cacheKey = $"catalog_product_{id}";

        if (_cache.TryGetValue(cacheKey, out ProductDto? cached) && cached != null)
        {
            _logger.LogDebug("Cache HIT для товару ID {Id}", id);
            return cached;
        }

        _logger.LogDebug("Cache MISS для товару ID {Id}", id);
        var product = await _inner.GetByIdAsync(id, ct);

        _cache.Set(cacheKey, product, DefaultCacheDuration);
        return product;
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, string createdBy = "system", CancellationToken ct = default)
    {
        var result = await _inner.CreateAsync(dto, createdBy, ct);
        InvalidateProduct(result.Id);
        return result;
    }

    public async Task UpdatePriceAsync(long id, UpdatePriceDto dto, string updatedBy = "system", CancellationToken ct = default)
    {
        await _inner.UpdatePriceAsync(id, dto, updatedBy, ct);
        InvalidateProduct(id);
    }

    public async Task RemoveStockAsync(long id, RemoveStockDto dto, CancellationToken ct = default)
    {
        await _inner.RemoveStockAsync(id, dto, ct);
        InvalidateProduct(id);
    }

    public async Task AddStockAsync(long id, AddStockDto dto, CancellationToken ct = default)
    {
        await _inner.AddStockAsync(id, dto, ct);
        InvalidateProduct(id);
    }

    public async Task DeleteAsync(long id, string deletedBy = "system", CancellationToken ct = default)
    {
        await _inner.DeleteAsync(id, deletedBy, ct);
        InvalidateProduct(id);
    }

    public async Task<IReadOnlyList<ProductDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
    {
        var cacheKey = $"catalog_products_category_{categoryId}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<ProductDto>? cached) && cached != null)
        {
            _logger.LogDebug("Cache HIT для категорії ID {CategoryId}", categoryId);
            return cached;
        }

        var result = await _inner.GetByCategoryAsync(categoryId, ct);
        _cache.Set(cacheKey, result, DefaultCacheDuration);
        return result;
    }

    private void InvalidateProduct(long id)
    {
        _cache.Remove($"catalog_product_{id}");
        _logger.LogDebug("Інвалідовано кеш для товару ID {Id}", id);
    }
}
