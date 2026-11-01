using Microsoft.Extensions.Logging;
using Sales.Bll.Dto;
using Sales.Bll.Services;

namespace Sales.Bll.Caching;

/// <summary>
/// Декоратор IDealService з дворівневим кешем (L1 пам'ять -> L2 Redis -> БД)
/// та синхронною інвалідацією після операцій запису.
/// </summary>
public sealed class CachedDealService : IDealService
{
    private readonly IDealService _inner;
    private readonly SalesTwoLevelCacheService _cache;
    private readonly ILogger<CachedDealService> _logger;

    private static readonly TimeSpan DealL1Ttl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DealL1Sliding = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DealL2Ttl = TimeSpan.FromMinutes(5);

    public const string StatsKey = "orders:stats";
    public static string OrderKey(long id) => $"orders:order:{id}";

    public CachedDealService(
        IDealService inner,
        SalesTwoLevelCacheService cache,
        ILogger<CachedDealService> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<DealDto?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _cache.GetOrSetAsync(
            OrderKey(id),
            () => _inner.GetByIdAsync(id, ct),
            l1Ttl: DealL1Ttl,
            l2Ttl: DealL2Ttl,
            l1Sliding: DealL1Sliding,
            size: 1,
            ct: ct);

    public Task<PagedResult<DealSummaryDto>> GetPageAsync(DealQueryDto query, CancellationToken ct = default) =>
        _inner.GetPageAsync(query, ct);

    public async Task<DealDto> CreateAsync(CreateDealDto dto, string createdBy, CancellationToken ct = default)
    {
        var created = await _inner.CreateAsync(dto, createdBy, ct);
        await InvalidateOrderAsync(created.Id, "OrderCreated", ct);
        return created;
    }

    public async Task<bool> AddItemAsync(long dealId, AddDealItemDto dto, string addedBy, CancellationToken ct = default)
    {
        var result = await _inner.AddItemAsync(dealId, dto, addedBy, ct);
        await InvalidateOrderAsync(dealId, "OrderItemAdded", ct);
        return result;
    }

    public async Task<bool> AdvanceAsync(long dealId, string changedBy, string? reason, CancellationToken ct = default)
    {
        var result = await _inner.AdvanceAsync(dealId, changedBy, reason, ct);
        await InvalidateOrderAsync(dealId, "OrderStageAdvanced", ct);
        return result;
    }

    public async Task<bool> WinDealAsync(long dealId, string performedBy, CancellationToken ct = default)
    {
        var result = await _inner.WinDealAsync(dealId, performedBy, ct);
        await InvalidateOrderAsync(dealId, "OrderConfirmed", ct);
        return result;
    }

    public async Task<bool> LoseAsync(long dealId, LoseDealDto dto, string changedBy, CancellationToken ct = default)
    {
        var result = await _inner.LoseAsync(dealId, dto, changedBy, ct);
        await InvalidateOrderAsync(dealId, "OrderLost", ct);
        return result;
    }

    public async Task<bool> SoftDeleteAsync(long id, string deletedBy, CancellationToken ct = default)
    {
        var result = await _inner.SoftDeleteAsync(id, deletedBy, ct);
        await InvalidateOrderAsync(id, "OrderDeleted", ct);
        return result;
    }

    public async Task<IReadOnlyList<DealStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        var result = await _cache.GetOrSetAsync(
            StatsKey,
            () => _inner.GetStatsAsync(ct)!,
            l1Ttl: TimeSpan.FromSeconds(30),
            l2Ttl: TimeSpan.FromMinutes(3),
            l1Sliding: TimeSpan.FromSeconds(15),
            size: 2,
            ct: ct);

        return result!;
    }

    private async Task InvalidateOrderAsync(long dealId, string reason, CancellationToken ct)
    {
        await _cache.RemoveAsync(OrderKey(dealId), reason, ct);
        await _cache.RemoveAsync(StatsKey, reason, ct);
        _logger.LogInformation("Cache invalidated {Key}, reason {Reason}", OrderKey(dealId), reason);
    }
}
