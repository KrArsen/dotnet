using Platform.EventBus.Events;
using Platform.EventBus.Metrics;
using Platform.EventBus.Topology;
using Sales.Dal.Interfaces;

namespace Sales.Api.IntegrationEvents;

/// <summary>
/// Ідемпотентний обробник ProductDeletedEvent у мікросервісі Sales (Orders):
/// позначає позицію неактивною (IsActive = 0) у локальному довіднику dbo.Products.
/// </summary>
public sealed class ProductDeletedHandler(
    IUnitOfWork uow,
    MessagingMetrics metrics,
    ILogger<ProductDeletedHandler> logger)
{
    public async Task HandleAsync(ProductDeletedEvent @event, CancellationToken ct = default)
    {
        if (@event.ProductId <= 0)
        {
            throw new ArgumentException($"Invalid ProductDeletedEvent payload: ProductId={@event.ProductId}");
        }

        await uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(@event.EventId, nameof(ProductDeletedEvent), ct))
            {
                metrics.DuplicateSkipped(RabbitMqTopology.OrdersCatalogSyncQueue, nameof(ProductDeletedEvent));
                logger.LogInformation(
                    "Duplicate event {EventId} ({EventType}) for Product {ProductId} skipped [CorrelationId: {CorrelationId}]",
                    @event.EventId,
                    nameof(ProductDeletedEvent),
                    @event.ProductId,
                    @event.CorrelationId);
                await uow.RollbackAsync(ct);
                return;
            }

            await uow.Products.DeactivateSnapshotAsync(@event.ProductId, @event.OccurredAt, ct);
            await uow.CommitAsync(ct);

            logger.LogInformation(
                "Replicated deactivation for Product {ProductId} ('{ProductName}') in SalesDb.Products (EventId: {EventId}, CorrelationId: {CorrelationId})",
                @event.ProductId,
                @event.ProductName,
                @event.EventId,
                @event.CorrelationId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to handle {EventType} {EventId} for Product {ProductId} [CorrelationId: {CorrelationId}], rolling back transaction",
                nameof(ProductDeletedEvent),
                @event.EventId,
                @event.ProductId,
                @event.CorrelationId);
            await uow.RollbackAsync(ct);
            throw;
        }
    }
}
