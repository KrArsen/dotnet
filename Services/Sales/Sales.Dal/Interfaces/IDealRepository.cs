using Sales.Domain.Models;

namespace Sales.Dal.Interfaces;

public interface IDealRepository : IGenericRepository<Deal>
{
    Task<Deal?> GetWithDetailsAsync(long id, CancellationToken ct = default);
    Task<(IReadOnlyList<DealSummary> Items, int TotalCount)> GetPageAsync(
        string? status,
        long? ownerId,
        long? clientId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<long> CreateAsync(Deal deal, DealDetails details, IEnumerable<DealItem> items, CancellationToken ct = default);
    Task<bool> AddItemAsync(long dealId, long productId, int quantity, decimal discount, string addedBy, CancellationToken ct = default);
    Task<bool> AdvanceAsync(long dealId, string changedBy, string? reason, CancellationToken ct = default);
    Task<bool> WinAsync(long dealId, string changedBy, string reason, CancellationToken ct = default);
    Task<bool> LoseAsync(long dealId, string reason, string changedBy, CancellationToken ct = default);
    Task<IReadOnlyList<DealStatusStat>> GetStatsByStatusAsync(CancellationToken ct = default);
}
