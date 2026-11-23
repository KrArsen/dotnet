using Activities.Application.Common.Interfaces;
using Activities.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Activities.Infrastructure.Repositories;

/// <summary>
/// MongoDB реалізація ідемпотентності (processed_events з унікальним ключем) та масового оновлення
/// денормалізованої назви товару (relatedProducts.$.name) в усіх відгуках/активностях за ProductId.
/// </summary>
public sealed class MongoProductReplicationStore(
    MongoDbContext context,
    ILogger<MongoProductReplicationStore> logger) : IProductReplicationStore
{
    public async Task<bool> TryMarkEventProcessedAsync(
        Guid eventId,
        string eventType,
        CancellationToken ct = default)
    {
        var idString = eventId.ToString();
        var doc = new ProcessedEventDocument
        {
            Id = idString,
            EventId = idString,
            EventType = eventType,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            await context.ProcessedEvents.InsertOneAsync(doc, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<long> UpdateProductNameInReviewsAsync(
        long productId,
        string newProductName,
        DateTime occurredAt,
        CancellationToken ct = default)
    {
        var rawCollection = context.Database.GetCollection<BsonDocument>("activities");

        // Фільтр: документи, що містять relatedProducts.productId == productId
        // та захист від out-of-order подій через lastProductSyncAt <= occurredAt
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("relatedProducts.productId", productId),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Exists("lastProductSyncAt", false),
                Builders<BsonDocument>.Filter.Lte("lastProductSyncAt", occurredAt)));

        var update = Builders<BsonDocument>.Update
            .Set("relatedProducts.$[p].name", newProductName)
            .Set("lastProductSyncAt", occurredAt)
            .Set("updatedAt", DateTime.UtcNow);

        var options = new UpdateOptions
        {
            ArrayFilters =
            [
                new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("p.productId", productId))
            ]
        };

        var result = await rawCollection.UpdateManyAsync(filter, update, options, ct);

        logger.LogInformation(
            "Updated productName to '{NewName}' for Product {ProductId} in {ModifiedCount}/{MatchedCount} MongoDB activity/review documents",
            newProductName,
            productId,
            result.ModifiedCount,
            result.MatchedCount);

        return result.ModifiedCount;
    }
}
