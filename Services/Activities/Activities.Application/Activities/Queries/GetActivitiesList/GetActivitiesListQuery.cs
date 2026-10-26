using Activities.Application.Common.Caching;
using Activities.Application.Common.Interfaces;
using Activities.Application.Common.Models;

namespace Activities.Application.Activities.Queries.GetActivitiesList;

/// <summary>
/// Запит вибірки списку активностей/відгуків з пагінацією та кешуванням на 30 секунд
/// </summary>
public record GetActivitiesListQuery(
    long? ClientId = null,
    string? Type = null,
    long? UserId = null,
    string? Search = null,
    long? ProductId = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ActivityDto>>, ICacheableQuery
{
    public string CacheKey => $"activities:c={ClientId}:t={Type}:u={UserId}:q={Search}:p={ProductId}:pg={Page}:sz={PageSize}";
    public TimeSpan? Expiration => TimeSpan.FromSeconds(30);
}
