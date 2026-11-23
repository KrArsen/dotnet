using System.Text.Json;
using Platform.EventBus.Consuming;
using Platform.EventBus.Events;
using Platform.EventBus.Metrics;
using Platform.EventBus.Serialization;
using Platform.EventBus.Topology;
using RabbitMQ.Client;

namespace Sales.Api.IntegrationEvents;

/// <summary>
/// Фоновий споживач подій Каталогу (orders.catalog-sync) у мікросервісі Sales/Orders.
/// Десеріалізує повідомлення та маршрутизує їх у відповідні Scoped-обробники.
/// </summary>
public sealed class CatalogEventsConsumer(
    IConnection connection,
    IServiceScopeFactory scopes,
    MessagingMetrics metrics,
    ILogger<CatalogEventsConsumer> logger)
    : RabbitMqConsumerBackgroundService(connection, metrics, logger)
{
    protected override string QueueName => RabbitMqTopology.OrdersCatalogSyncQueue;

    protected override async Task HandleMessageAsync(
        string eventType,
        ReadOnlyMemory<byte> body,
        IReadOnlyBasicProperties properties,
        string? correlationId,
        CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();

        switch (eventType)
        {
            case nameof(ProductPriceChangedEvent):
            {
                var @event = JsonSerializer.Deserialize<ProductPriceChangedEvent>(body.Span, EventBusJsonOptions.Default)
                    ?? throw new JsonException("Empty ProductPriceChangedEvent payload");
                var handler = scope.ServiceProvider.GetRequiredService<ProductPriceChangedHandler>();
                await handler.HandleAsync(@event, ct);
                break;
            }
            case nameof(ProductRenamedEvent):
            {
                var @event = JsonSerializer.Deserialize<ProductRenamedEvent>(body.Span, EventBusJsonOptions.Default)
                    ?? throw new JsonException("Empty ProductRenamedEvent payload");
                var handler = scope.ServiceProvider.GetRequiredService<ProductRenamedHandler>();
                await handler.HandleAsync(@event, ct);
                break;
            }
            case nameof(ProductCreatedEvent):
            {
                var @event = JsonSerializer.Deserialize<ProductCreatedEvent>(body.Span, EventBusJsonOptions.Default)
                    ?? throw new JsonException("Empty ProductCreatedEvent payload");
                var handler = scope.ServiceProvider.GetRequiredService<ProductCreatedHandler>();
                await handler.HandleAsync(@event, ct);
                break;
            }
            case nameof(ProductDeletedEvent):
            {
                var @event = JsonSerializer.Deserialize<ProductDeletedEvent>(body.Span, EventBusJsonOptions.Default)
                    ?? throw new JsonException("Empty ProductDeletedEvent payload");
                var handler = scope.ServiceProvider.GetRequiredService<ProductDeletedHandler>();
                await handler.HandleAsync(@event, ct);
                break;
            }
            default:
                throw new NotSupportedException($"Unsupported catalog event type '{eventType}' on queue '{QueueName}'.");
        }
    }
}
