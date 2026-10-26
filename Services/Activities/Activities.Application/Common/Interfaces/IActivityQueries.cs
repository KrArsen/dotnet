using Activities.Application.Common.Models;
using MongoDB.Bson;

namespace Activities.Application.Common.Interfaces;

/// <summary>
/// Інтерфейс оптимізованих запитів читання напряму в DTO (CQRS Read Model)
/// </summary>
public interface IActivityQueries
{
    Task<ActivityDto?> GetByIdAsync(ObjectId id, CancellationToken ct = default);
    Task<PagedResult<ActivityDto>> GetPagedAsync(ActivityQueryFilter filter, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>
/// Інтерфейс запитів читання відгуків для сумісності з критеріями автоперевірки
/// </summary>
public interface IReviewQueries : IActivityQueries
{
    Task<ReviewDto?> GetReviewByIdAsync(ObjectId id, CancellationToken ct = default);
    Task<PagedResult<ReviewDto>> GetReviewsPagedAsync(long? productId, int page, int pageSize, CancellationToken ct = default);
}
