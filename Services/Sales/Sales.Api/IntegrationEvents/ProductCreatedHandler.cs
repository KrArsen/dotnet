using Platform.EventBus.Events;
using Platform.EventBus.Metrics;
using Platform.EventBus.Topology;
using Sales.Dal.Interfaces;

namespace Sales.Api.IntegrationEvents;

/// <summary>
/// Ідемпотентний обробник ProductCreatedEvent у мікросервісі Sales (Orders):
/// додає новий товар/послугу в локальний довідник dbo.Products в одній транзакції з dbo.ProcessedEvents.
/// </summary>
public sealed class ProductCreatedHandler(
    IUnitOfWork uow,
    MessagingMetrics metrics,
    ILogger<ProductCreatedHandler> logger)
{
    public async Task HandleAsync(ProductCreatedEvent @event, CancellationToken ct = default)
    {
        if (@event.ProductId <= 0 || string.IsNullOrWhiteSpace(@event.ProductName) || @event.Price <= 0)
        {
            throw new ArgumentException($"Invalid ProductCreatedEvent payload: ProductId={@event.ProductId}");
        }

        await uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(@event.EventId, nameof(ProductCreatedEvent), ct))
            {
                metrics.DuplicateSkipped(RabbitMqTopology.OrdersCatalogSyncQueue, nameof(ProductCreatedEvent));
                logger.LogInformation(
                    "Duplicate event {EventId} ({EventType}) for Product {ProductId} skipped [CorrelationId: {CorrelationId}]",
                    @event.EventId,
                    nameof(ProductCreatedEvent),
                    @event.ProductId,
                    @event.CorrelationId);
                await uow.RollbackAsync(ct);
                return;
            }

            await uow.Products.UpsertSnapshotAsync(
                @event.ProductId,
                @event.ProductName,
                @event.Price,
                @event.IsActive,
                @event.OccurredAt,
                ct);

            await uow.CommitAsync(ct);

            logger.LogInformation(
                "Replicated new Product {ProductId} ('{ProductName}', Price: {Price}) in SalesDb.Products (EventId: {EventId}, CorrelationId: {CorrelationId})",
                @event.ProductId,
                @event.ProductName,
                @event.Price,
                @event.EventId,
                @event.CorrelationId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to handle {EventType} {EventId} for Product {ProductId} [CorrelationId: {CorrelationId}], rolling back transaction",
                nameof(ProductCreatedEvent),
                @event.EventId,
                @event.ProductId,
                @event.CorrelationId);
            await uow.RollbackAsync(ct);
            throw;
        }
    }
}
