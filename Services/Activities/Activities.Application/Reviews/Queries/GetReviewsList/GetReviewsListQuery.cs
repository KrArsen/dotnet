using Activities.Application.Common.Caching;
using Activities.Application.Common.Interfaces;
using Activities.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace Activities.Application.Reviews.Queries.GetReviewsList;

public record GetReviewsListQuery(
    long? ProductId = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ReviewDto>>, ICacheableQuery
{
    public string CacheKey => $"reviews:prod={ProductId}:page={Page}:size={PageSize}";
    public TimeSpan? Expiration => TimeSpan.FromSeconds(30);
}

public class GetReviewsListQueryHandler : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewDto>>
{
    private readonly IReviewQueries _queries;

    public GetReviewsListQueryHandler(IReviewQueries queries)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<PagedResult<ReviewDto>> Handle(GetReviewsListQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        return await _queries.GetReviewsPagedAsync(request.ProductId, page, pageSize, cancellationToken);
    }
}

public class GetReviewsListQueryValidator : AbstractValidator<GetReviewsListQuery>
{
    public GetReviewsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        When(x => x.ProductId.HasValue, () => RuleFor(x => x.ProductId!.Value).GreaterThan(0));
    }
}
