using Platform.EventBus.Events;
using Platform.EventBus.Metrics;
using Platform.EventBus.Topology;
using Sales.Dal.Interfaces;

namespace Sales.Api.IntegrationEvents;

/// <summary>
/// Ідемпотентний обробник ProductPriceChangedEvent у мікросервісі Sales (Orders):
/// - у спільній транзакції UnitOfWork перевіряє/реєструє EventId у dbo.ProcessedEvents;
/// - оновлює локальну копію довідника dbo.Products (політика дублювання Лаб. 01);
/// - НЕ змінює ціни в уже оформлених угодах (dbo.DealItems.UnitPrice — історичний знімок).
/// </summary>
public sealed class ProductPriceChangedHandler(
    IUnitOfWork uow,
    MessagingMetrics metrics,
    ILogger<ProductPriceChangedHandler> logger)
{
    public async Task HandleAsync(ProductPriceChangedEvent @event, CancellationToken ct = default)
    {
        if (@event.ProductId <= 0 || @event.NewPrice <= 0)
        {
            throw new ArgumentException($"Invalid ProductPriceChangedEvent payload: ProductId={@event.ProductId}, NewPrice={@event.NewPrice}");
        }

        await uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(@event.EventId, nameof(ProductPriceChangedEvent), ct))
            {
                metrics.DuplicateSkipped(RabbitMqTopology.OrdersCatalogSyncQueue, nameof(ProductPriceChangedEvent));
                logger.LogInformation(
                    "Duplicate event {EventId} ({EventType}) for Product {ProductId} skipped [CorrelationId: {CorrelationId}]",
                    @event.EventId,
                    nameof(ProductPriceChangedEvent),
                    @event.ProductId,
                    @event.CorrelationId);
                await uow.RollbackAsync(ct);
                return;
            }

            await uow.Products.UpdateSnapshotAsync(
                @event.ProductId,
                @event.ProductName,
                @event.NewPrice,
                @event.OccurredAt,
                ct);

            await uow.CommitAsync(ct);

            logger.LogInformation(
                "Replicated price change for Product {ProductId} ('{ProductName}'): {OldPrice} -> {NewPrice} in SalesDb.Products (EventId: {EventId}, CorrelationId: {CorrelationId}). Historical DealItems.UnitPrice unchanged.",
                @event.ProductId,
                @event.ProductName,
                @event.OldPrice,
                @event.NewPrice,
                @event.EventId,
                @event.CorrelationId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to handle {EventType} {EventId} for Product {ProductId} [CorrelationId: {CorrelationId}], rolling back transaction",
                nameof(ProductPriceChangedEvent),
                @event.EventId,
                @event.ProductId,
                @event.CorrelationId);
            await uow.RollbackAsync(ct);
            throw;
        }
    }
}
