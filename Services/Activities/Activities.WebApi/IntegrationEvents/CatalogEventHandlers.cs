using Activities.Application.Common.Caching;
using Activities.Application.Common.Interfaces;
using Platform.EventBus.Events;
using Platform.EventBus.Metrics;
using Platform.EventBus.Topology;

namespace Activities.WebApi.IntegrationEvents;

/// <summary>
/// Ідемпотентний обробник ProductRenamedEvent у сервісі Reviews/Activities:
/// - перевіряє унікальність EventId у MongoDB-колекції processed_events;
/// - оновлює денормалізоване поле productName (relatedProducts.$.name) в усіх документах відгуків/активностей;
/// - інвалідує версію кешу запитів (ICacheVersionProvider).
/// </summary>
public sealed class ProductRenamedHandler(
    IProductReplicationStore replicationStore,
    ICacheVersionProvider cacheVersionProvider,
    MessagingMetrics metrics,
    ILogger<ProductRenamedHandler> logger)
{
    public async Task HandleAsync(ProductRenamedEvent @event, CancellationToken ct = default)
    {
        if (@event.ProductId <= 0 || string.IsNullOrWhiteSpace(@event.NewName))
        {
            throw new ArgumentException($"Invalid ProductRenamedEvent payload: ProductId={@event.ProductId}, NewName='{@event.NewName}'");
        }

        if (!await replicationStore.TryMarkEventProcessedAsync(@event.EventId, nameof(ProductRenamedEvent), ct))
        {
            metrics.DuplicateSkipped(RabbitMqTopology.ReviewsCatalogSyncQueue, nameof(ProductRenamedEvent));
            logger.LogInformation(
                "Duplicate event {EventId} ({EventType}) for Product {ProductId} skipped in Reviews [CorrelationId: {CorrelationId}]",
                @event.EventId,
                nameof(ProductRenamedEvent),
                @event.ProductId,
                @event.CorrelationId);
            return;
        }

        var modified = await replicationStore.UpdateProductNameInReviewsAsync(
            @event.ProductId,
            @event.NewName,
            @event.OccurredAt,
            ct);

        cacheVersionProvider.Invalidate();

        logger.LogInformation(
            "Replicated product rename in Reviews/Activities for Product {ProductId}: '{OldName}' -> '{NewName}' (UpdatedDocuments: {ModifiedCount}, EventId: {EventId}, CorrelationId: {CorrelationId})",
            @event.ProductId,
            @event.OldName,
            @event.NewName,
            modified,
            @event.EventId,
            @event.CorrelationId);
    }
}

/// <summary>
/// Ідемпотентний обробник ProductPriceChangedEvent у сервісі Reviews/Activities:
/// фіксує EventId у processed_events, оновлює productName за потреби та інвалідує кеш.
/// </summary>
public sealed class ProductPriceChangedHandler(
    IProductReplicationStore replicationStore,
    ICacheVersionProvider cacheVersionProvider,
    MessagingMetrics metrics,
    ILogger<ProductPriceChangedHandler> logger)
{
    public async Task HandleAsync(ProductPriceChangedEvent @event, CancellationToken ct = default)
    {
        if (@event.ProductId <= 0)
        {
            throw new ArgumentException($"Invalid ProductPriceChangedEvent payload: ProductId={@event.ProductId}");
        }

        if (!await replicationStore.TryMarkEventProcessedAsync(@event.EventId, nameof(ProductPriceChangedEvent), ct))
        {
            metrics.DuplicateSkipped(RabbitMqTopology.ReviewsCatalogSyncQueue, nameof(ProductPriceChangedEvent));
            logger.LogInformation(
                "Duplicate event {EventId} ({EventType}) for Product {ProductId} skipped in Reviews [CorrelationId: {CorrelationId}]",
                @event.EventId,
                nameof(ProductPriceChangedEvent),
                @event.ProductId,
                @event.CorrelationId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(@event.ProductName))
        {
            await replicationStore.UpdateProductNameInReviewsAsync(
                @event.ProductId,
                @event.ProductName,
                @event.OccurredAt,
                ct);
        }

        cacheVersionProvider.Invalidate();

        logger.LogInformation(
            "Processed {EventType} {EventId} for Product {ProductId} in Reviews (NewPrice: {NewPrice}, CorrelationId: {CorrelationId})",
            nameof(ProductPriceChangedEvent),
            @event.EventId,
            @event.ProductId,
            @event.NewPrice,
            @event.CorrelationId);
    }
}

/// <summary>
/// Ідемпотентний обробник ProductCreatedEvent / ProductDeletedEvent у сервісі Reviews/Activities.
/// </summary>
public sealed class ProductLifecycleHandler(
    IProductReplicationStore replicationStore,
    ICacheVersionProvider cacheVersionProvider,
    MessagingMetrics metrics,
    ILogger<ProductLifecycleHandler> logger)
{
    public async Task HandleCreatedAsync(ProductCreatedEvent @event, CancellationToken ct = default)
    {
        if (!await replicationStore.TryMarkEventProcessedAsync(@event.EventId, nameof(ProductCreatedEvent), ct))
        {
            metrics.DuplicateSkipped(RabbitMqTopology.ReviewsCatalogSyncQueue, nameof(ProductCreatedEvent));
            return;
        }

        logger.LogInformation(
            "Recorded {EventType} {EventId} for Product {ProductId} ('{ProductName}') in Reviews [CorrelationId: {CorrelationId}]",
            nameof(ProductCreatedEvent),
            @event.EventId,
            @event.ProductId,
            @event.ProductName,
            @event.CorrelationId);
    }

    public async Task HandleDeletedAsync(ProductDeletedEvent @event, CancellationToken ct = default)
    {
        if (!await replicationStore.TryMarkEventProcessedAsync(@event.EventId, nameof(ProductDeletedEvent), ct))
        {
            metrics.DuplicateSkipped(RabbitMqTopology.ReviewsCatalogSyncQueue, nameof(ProductDeletedEvent));
            return;
        }

        cacheVersionProvider.Invalidate();

        logger.LogInformation(
            "Recorded {EventType} {EventId} for archived Product {ProductId} in Reviews [CorrelationId: {CorrelationId}]",
            nameof(ProductDeletedEvent),
            @event.EventId,
            @event.ProductId,
            @event.CorrelationId);
    }
}
