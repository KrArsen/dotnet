using Activities.Application.Common.Interfaces;
using Activities.Application.Common.Models;
using Activities.Domain.Entities;
using Activities.Infrastructure.Persistence;
using AutoMapper;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Activities.Infrastructure.Queries;

public class ActivityQueries : IActivityQueries, IReviewQueries
{
    private readonly IMongoCollection<Activity> _collection;
    private readonly IMapper _mapper;

    public ActivityQueries(MongoDbContext context, IMapper mapper)
    {
        _collection = context.Activities ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<ActivityDto?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
    {
        var activity = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        return activity != null ? _mapper.Map<ActivityDto>(activity) : null;
    }

    public async Task<PagedResult<ActivityDto>> GetPagedAsync(
        ActivityQueryFilter filter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var filterDef = BuildFilter(filter);

        var totalCount = await _collection.CountDocumentsAsync(filterDef, cancellationToken: ct);

        var skip = (page - 1) * pageSize;
        var entities = await _collection
            .Find(filterDef)
            .SortByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync(ct);

        var dtos = _mapper.Map<IReadOnlyList<ActivityDto>>(entities);
        return new PagedResult<ActivityDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ReviewDto?> GetReviewByIdAsync(ObjectId id, CancellationToken ct = default)
    {
        var activity = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        return activity != null ? _mapper.Map<ReviewDto>(activity) : null;
    }

    public async Task<PagedResult<ReviewDto>> GetReviewsPagedAsync(
        long? productId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var filterBuilder = Builders<Activity>.Filter;
        var filterDef = filterBuilder.Empty;

        if (productId.HasValue)
        {
            var byProduct = filterBuilder.ElemMatch(x => x.RelatedProducts, p => p.ProductId == productId.Value);
            var byClient = filterBuilder.Eq(x => x.ClientId, productId.Value);
            filterDef = filterBuilder.Or(byProduct, byClient);
        }

        var totalCount = await _collection.CountDocumentsAsync(filterDef, cancellationToken: ct);

        var skip = (page - 1) * pageSize;
        var entities = await _collection
            .Find(filterDef)
            .SortByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync(ct);

        var dtos = _mapper.Map<IReadOnlyList<ReviewDto>>(entities);
        return new PagedResult<ReviewDto>(dtos, totalCount, page, pageSize);
    }

    private static FilterDefinition<Activity> BuildFilter(ActivityQueryFilter filter)
    {
        var builder = Builders<Activity>.Filter;
        var filters = new List<FilterDefinition<Activity>>();

        if (filter.ClientId.HasValue)
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            filters.Add(builder.Eq(x => x.Type, filter.Type.Trim().ToLowerInvariant()));
        }

        if (filter.UserId.HasValue)
        {
            filters.Add(builder.Eq("author.userId", filter.UserId.Value));
        }

        if (filter.ProductId.HasValue)
        {
            filters.Add(builder.ElemMatch(x => x.RelatedProducts, p => p.ProductId == filter.ProductId.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            var textFilter = builder.Or(
                builder.Regex(x => x.Text, new BsonRegularExpression(search, "i")),
                builder.Regex(x => x.ClientName, new BsonRegularExpression(search, "i")));
            filters.Add(textFilter);
        }

        return filters.Count > 0 ? builder.And(filters) : builder.Empty;
    }
}
