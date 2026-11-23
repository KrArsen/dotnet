using System.Diagnostics;
using Catalog.Dal.Context;
using Catalog.Domain.Entities;
using Platform.EventBus.Events;
using Platform.EventBus.Publishing;
using Platform.EventBus.Serialization;
using Platform.EventBus.Topology;
using System.Text.Json;

namespace Catalog.Api.IntegrationEvents;

/// <summary>
/// Фасад інтеграційних подій Каталогу (IntegrationEvents):
/// підтримує як пряму публікацію після SaveChangesAsync, так і запис у таблицю OutboxMessages.
/// </summary>
public interface ICatalogIntegrationEventService
{
    Task PublishPriceChangedAsync(long productId, string productName, decimal oldPrice, decimal newPrice, CancellationToken ct = default);
    Task PublishRenamedAsync(long productId, string oldName, string newName, decimal currentPrice, CancellationToken ct = default);
    Task SaveToOutboxAsync<T>(T @event, string routingKey, CancellationToken ct = default) where T : IntegrationEvent;
}

public sealed class CatalogIntegrationEventService(
    IEventPublisher publisher,
    AppDbContext dbContext,
    ILogger<CatalogIntegrationEventService> logger) : ICatalogIntegrationEventService
{
    public async Task PublishPriceChangedAsync(
        long productId,
        string productName,
        decimal oldPrice,
        decimal newPrice,
        CancellationToken ct = default)
    {
        var @event = new ProductPriceChangedEvent(productId, productName, oldPrice, newPrice)
        {
            CorrelationId = Activity.Current?.TraceId.ToString()
        };

        try
        {
            await publisher.PublishAsync(
                @event,
                RabbitMqTopology.CatalogExchange,
                RabbitMqTopology.ProductPriceChangedRoutingKey,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish {EventType} {EventId} for Product {ProductId} [CorrelationId: {CorrelationId}]",
                nameof(ProductPriceChangedEvent),
                @event.EventId,
                productId,
                @event.CorrelationId);
            throw;
        }
    }

    public async Task PublishRenamedAsync(
        long productId,
        string oldName,
        string newName,
        decimal currentPrice,
        CancellationToken ct = default)
    {
        var @event = new ProductRenamedEvent(productId, oldName, newName, currentPrice)
        {
            CorrelationId = Activity.Current?.TraceId.ToString()
        };

        try
        {
            await publisher.PublishAsync(
                @event,
                RabbitMqTopology.CatalogExchange,
                RabbitMqTopology.ProductRenamedRoutingKey,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish {EventType} {EventId} for Product {ProductId} [CorrelationId: {CorrelationId}]",
                nameof(ProductRenamedEvent),
                @event.EventId,
                productId,
                @event.CorrelationId);
            throw;
        }
    }

    public async Task SaveToOutboxAsync<T>(T @event, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent
    {
        var payload = JsonSerializer.Serialize(@event, EventBusJsonOptions.Default);
        var outbox = new OutboxMessage(
            @event.EventId,
            typeof(T).Name,
            RabbitMqTopology.CatalogExchange,
            routingKey,
            payload,
            @event.CorrelationId ?? Activity.Current?.TraceId.ToString());

        await dbContext.OutboxMessages.AddAsync(outbox, ct);
        logger.LogInformation(
            "Saved {EventType} {EventId} to OutboxMessages (RoutingKey: {RoutingKey})",
            typeof(T).Name,
            @event.EventId,
            routingKey);
    }
}
