using Platform.EventBus.Events;
using Platform.EventBus.Metrics;
using Platform.EventBus.Topology;
using Sales.Dal.Interfaces;

namespace Sales.Api.IntegrationEvents;

/// <summary>
/// Ідемпотентний обробник ProductRenamedEvent у мікросервісі Sales (Orders):
/// оновлює локальну назву товару в довіднику dbo.Products в одній транзакції з dbo.ProcessedEvents.
/// </summary>
public sealed class ProductRenamedHandler(
    IUnitOfWork uow,
    MessagingMetrics metrics,
    ILogger<ProductRenamedHandler> logger)
{
    public async Task HandleAsync(ProductRenamedEvent @event, CancellationToken ct = default)
    {
        if (@event.ProductId <= 0 || string.IsNullOrWhiteSpace(@event.NewName))
        {
            throw new ArgumentException($"Invalid ProductRenamedEvent payload: ProductId={@event.ProductId}, NewName='{@event.NewName}'");
        }

        await uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(@event.EventId, nameof(ProductRenamedEvent), ct))
            {
                metrics.DuplicateSkipped(RabbitMqTopology.OrdersCatalogSyncQueue, nameof(ProductRenamedEvent));
                logger.LogInformation(
                    "Duplicate event {EventId} ({EventType}) for Product {ProductId} skipped [CorrelationId: {CorrelationId}]",
                    @event.EventId,
                    nameof(ProductRenamedEvent),
                    @event.ProductId,
                    @event.CorrelationId);
                await uow.RollbackAsync(ct);
                return;
            }

            await uow.Products.UpdateSnapshotAsync(
                @event.ProductId,
                @event.NewName,
                @event.CurrentPrice,
                @event.OccurredAt,
                ct);

            await uow.CommitAsync(ct);

            logger.LogInformation(
                "Replicated rename for Product {ProductId}: '{OldName}' -> '{NewName}' in SalesDb.Products (EventId: {EventId}, CorrelationId: {CorrelationId})",
                @event.ProductId,
                @event.OldName,
                @event.NewName,
                @event.EventId,
                @event.CorrelationId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to handle {EventType} {EventId} for Product {ProductId} [CorrelationId: {CorrelationId}], rolling back transaction",
                nameof(ProductRenamedEvent),
                @event.EventId,
                @event.ProductId,
                @event.CorrelationId);
            await uow.RollbackAsync(ct);
            throw;
        }
    }
}
