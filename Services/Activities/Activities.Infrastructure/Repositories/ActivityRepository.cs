using Activities.Domain.Entities;
using Activities.Domain.Exceptions;
using Activities.Domain.Interfaces;
using Activities.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Activities.Infrastructure.Repositories;

/// <summary>
/// Репозиторій агрегату Activity на базі MongoDB C# Driver з підтримкою оптимістичної конкурентності
/// </summary>
public class ActivityRepository : IActivityRepository, IReviewRepository
{
    private readonly IMongoCollection<Activity> _collection;

    public ActivityRepository(MongoDbContext context)
    {
        _collection = context.Activities ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Activity?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
    {
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsAsync(long clientId, long userId, CancellationToken ct = default)
    {
        return await _collection.Find(x => x.ClientId == clientId && x.Author.UserId == userId).AnyAsync(ct);
    }

    public async Task AddAsync(Activity entity, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(entity, null, ct);
    }

    public async Task UpdateAsync(Activity entity, int expectedVersion, CancellationToken ct = default)
    {
        var filter = Builders<Activity>.Filter.And(
            Builders<Activity>.Filter.Eq(x => x.Id, entity.Id),
            Builders<Activity>.Filter.Eq(x => x.Version, expectedVersion));

        var result = await _collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);

        if (result.MatchedCount == 0)
        {
            var current = await _collection.Find(x => x.Id == entity.Id).FirstOrDefaultAsync(ct);
            if (current == null)
            {
                throw new NotFoundException(nameof(Activity), entity.Id);
            }

            throw new ConflictException(nameof(Activity), entity.Id, expectedVersion, current.Version);
        }
    }

    public async Task DeleteAsync(ObjectId id, CancellationToken ct = default)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id, ct);
        if (result.DeletedCount == 0)
        {
            throw new NotFoundException(nameof(Activity), id);
        }
    }
}
