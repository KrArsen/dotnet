using System.Text.Json;
using Platform.EventBus.Consuming;
using Platform.EventBus.Events;
using Platform.EventBus.Metrics;
using Platform.EventBus.Serialization;
using Platform.EventBus.Topology;
using RabbitMQ.Client;

namespace Activities.WebApi.IntegrationEvents;

/// <summary>
/// Фоновий споживач подій Каталогу (reviews.catalog-sync) у мікросервісі Activities/Reviews.
/// Оновлює денормалізовану назву товару в документах MongoDB та інвалідує кеш.
/// </summary>
public sealed class ReviewsCatalogEventsConsumer(
    IConnection connection,
    IServiceScopeFactory scopes,
    MessagingMetrics metrics,
    ILogger<ReviewsCatalogEventsConsumer> logger)
    : RabbitMqConsumerBackgroundService(connection, metrics, logger)
{
    protected override string QueueName => RabbitMqTopology.ReviewsCatalogSyncQueue;

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
            case nameof(ProductRenamedEvent):
            {
                var @event = JsonSerializer.Deserialize<ProductRenamedEvent>(body.Span, EventBusJsonOptions.Default)
                    ?? throw new JsonException("Empty ProductRenamedEvent payload");
                var handler = scope.ServiceProvider.GetRequiredService<ProductRenamedHandler>();
                await handler.HandleAsync(@event, ct);
                break;
            }
            case nameof(ProductPriceChangedEvent):
            {
                var @event = JsonSerializer.Deserialize<ProductPriceChangedEvent>(body.Span, EventBusJsonOptions.Default)
                    ?? throw new JsonException("Empty ProductPriceChangedEvent payload");
                var handler = scope.ServiceProvider.GetRequiredService<ProductPriceChangedHandler>();
                await handler.HandleAsync(@event, ct);
                break;
            }
            case nameof(ProductCreatedEvent):
            {
                var @event = JsonSerializer.Deserialize<ProductCreatedEvent>(body.Span, EventBusJsonOptions.Default)
                    ?? throw new JsonException("Empty ProductCreatedEvent payload");
                var handler = scope.ServiceProvider.GetRequiredService<ProductLifecycleHandler>();
                await handler.HandleCreatedAsync(@event, ct);
                break;
            }
            case nameof(ProductDeletedEvent):
            {
                var @event = JsonSerializer.Deserialize<ProductDeletedEvent>(body.Span, EventBusJsonOptions.Default)
                    ?? throw new JsonException("Empty ProductDeletedEvent payload");
                var handler = scope.ServiceProvider.GetRequiredService<ProductLifecycleHandler>();
                await handler.HandleDeletedAsync(@event, ct);
                break;
            }
            default:
                throw new NotSupportedException($"Unsupported catalog event type '{eventType}' on queue '{QueueName}'.");
        }
    }
}
