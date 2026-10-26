using Activities.Application.Common.Interfaces;
using Activities.Application.Common.Models;
using MediatR;

namespace Activities.Application.Activities.Queries.GetActivitiesList;

public class GetActivitiesListQueryHandler : IRequestHandler<GetActivitiesListQuery, PagedResult<ActivityDto>>
{
    private readonly IActivityQueries _queries;

    public GetActivitiesListQueryHandler(IActivityQueries queries)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<PagedResult<ActivityDto>> Handle(GetActivitiesListQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var filter = new ActivityQueryFilter(
            ClientId: request.ClientId,
            Type: request.Type,
            UserId: request.UserId,
            SearchText: request.Search,
            ProductId: request.ProductId);

        return await _queries.GetPagedAsync(filter, page, pageSize, cancellationToken);
    }
}
