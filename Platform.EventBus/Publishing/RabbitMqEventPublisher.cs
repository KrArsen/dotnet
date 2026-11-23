using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;
using Platform.EventBus.Metrics;
using Platform.EventBus.Serialization;
using Platform.EventBus.Topology;
using Platform.EventBus.Tracing;
using RabbitMQ.Client;

namespace Platform.EventBus.Publishing;

/// <summary>
/// Реалізація публікації подій у RabbitMQ (RabbitMQ.Client 7.x, асинхронний IChannel):
/// - серіалізація в JSON (camelCase, System.Text.Json);
/// - persistent-доставка (Persistent = true), ContentType = application/json, MessageId = EventId, Type = назва події;
/// - передача заголовків x-correlation-id, x-schema-version, x-event-type та W3C traceparent через TracePropagation.Inject.
/// </summary>
public sealed class RabbitMqEventPublisher(
    IConnection connection,
    MessagingMetrics metrics,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher
{
    private volatile bool _topologyDeclared;
    private readonly SemaphoreSlim _topologyLock = new(1, 1);

    public async Task PublishAsync<T>(
        T @event,
        string exchange,
        string routingKey,
        CancellationToken ct = default)
        where T : IntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var eventType = typeof(T).Name;
        var correlationId = @event.CorrelationId ?? Activity.Current?.TraceId.ToString();

        using var activity = TracePropagation.StartProducerActivity(exchange, routingKey, eventType, @event.EventId);

        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        if (!_topologyDeclared)
        {
            await EnsureTopologyAsync(channel, ct);
        }

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = @event.EventId.ToString(),
            Type = eventType,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(@event.OccurredAt).ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                [TracePropagation.CorrelationIdHeader] = correlationId ?? string.Empty,
                ["x-event-type"] = eventType,
                ["x-schema-version"] = @event.SchemaVersion
            }
        };

        // Запис W3C traceparent / tracestate у заголовки повідомлення для наскрізного трасування
        TracePropagation.Inject(props);

        var body = JsonSerializer.SerializeToUtf8Bytes(@event, EventBusJsonOptions.Default);

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);

        metrics.Published(exchange, eventType);

        logger.LogInformation(
            "Published {EventType} {EventId} to {Exchange}/{RoutingKey} [CorrelationId: {CorrelationId}]",
            eventType,
            @event.EventId,
            exchange,
            routingKey,
            correlationId);
    }

    private async Task EnsureTopologyAsync(IChannel channel, CancellationToken ct)
    {
        await _topologyLock.WaitAsync(ct);
        try
        {
            if (_topologyDeclared)
            {
                return;
            }

            await RabbitMqTopology.DeclareTopologyAsync(channel, ct);
            _topologyDeclared = true;
        }
        finally
        {
            _topologyLock.Release();
        }
    }
}
