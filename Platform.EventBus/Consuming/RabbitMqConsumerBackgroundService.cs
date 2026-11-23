using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Metrics;
using Platform.EventBus.Topology;
using Platform.EventBus.Tracing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace Platform.EventBus.Consuming;

/// <summary>
/// Базовий фоновий споживач RabbitMQ (BackgroundService, RabbitMQ.Client 7.x):
/// - декларативно оголошує топологію при старті;
/// - встановлює BasicQos (prefetchCount = 10) та BasicConsumeAsync з autoAck: false;
/// - відновлює W3C TraceContext (ActivityKind.Consumer) та CorrelationId у Serilog LogContext;
/// - виконує BasicAckAsync після успішної обробки;
/// - при постійному збої (JsonException / несумісна версія / невідомий тип) одразу відправляє в DLQ (BasicNackAsync requeue: false);
/// - при тимчасовому збої перепубліковує повідомлення зі збільшеним заголовком x-retry-count до MaxRetries, після чого відправляє в DLQ.
/// </summary>
public abstract class RabbitMqConsumerBackgroundService(
    IConnection connection,
    MessagingMetrics metrics,
    ILogger logger) : BackgroundService
{
    protected const int DefaultMaxRetries = 3;
    protected const int SupportedSchemaVersion = 1;

    protected abstract string QueueName { get; }
    protected virtual int MaxRetries => DefaultMaxRetries;
    protected virtual ushort PrefetchCount => 10;

    private IChannel? _channel;
    private string? _consumerTag;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting RabbitMQ consumer for queue {Queue}", QueueName);

        try
        {
            _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await RabbitMqTopology.DeclareTopologyAsync(_channel, stoppingToken);
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += OnMessageAsync;

            _consumerTag = await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            logger.LogInformation(
                "RabbitMQ consumer subscribed to queue {Queue} (PrefetchCount: {PrefetchCount}, MaxRetries: {MaxRetries})",
                QueueName,
                PrefetchCount,
                MaxRetries);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("RabbitMQ consumer for queue {Queue} stopping gracefully", QueueName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RabbitMQ consumer for queue {Queue} encountered an initialization error", QueueName);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_channel is not null && !string.IsNullOrWhiteSpace(_consumerTag) && _channel.IsOpen)
            {
                await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
            }

            if (_channel is not null)
            {
                await _channel.CloseAsync(cancellationToken);
                await _channel.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while closing RabbitMQ channel for queue {Queue}", QueueName);
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Бізнес-обробка конкретного повідомлення у дочірньому сервісі-споживачі.
    /// </summary>
    protected abstract Task HandleMessageAsync(
        string eventType,
        ReadOnlyMemory<byte> body,
        IReadOnlyBasicProperties properties,
        string? correlationId,
        CancellationToken ct);

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs ea)
    {
        // У RabbitMQ.Client 7.x буфер ea.Body належить клієнту й може бути перевикористаний — копіюємо одразу
        var bodyCopy = ea.Body.ToArray();
        var messageId = ea.BasicProperties.MessageId ?? "unknown";
        var eventType = ResolveEventType(ea);
        var correlationId = TracePropagation.ExtractHeaderString(ea.BasicProperties.Headers, TracePropagation.CorrelationIdHeader)
                            ?? Activity.Current?.TraceId.ToString();

        using var activity = TracePropagation.StartConsumerActivity(ea, QueueName);
        using var _ = LogContext.PushProperty("CorrelationId", correlationId ?? string.Empty);

        var sw = Stopwatch.StartNew();

        try
        {
            ValidateSchemaVersion(ea.BasicProperties.Headers);

            logger.LogInformation(
                "Received {EventType} (MessageId: {MessageId}, RoutingKey: {RoutingKey}, Queue: {Queue}, CorrelationId: {CorrelationId})",
                eventType,
                messageId,
                ea.RoutingKey,
                QueueName,
                correlationId);

            await HandleMessageAsync(eventType, bodyCopy, ea.BasicProperties, correlationId, CancellationToken.None);

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);

            sw.Stop();
            metrics.Consumed(QueueName, eventType, success: true);
            metrics.RecordProcessingDuration(QueueName, success: true, sw.Elapsed.TotalMilliseconds);

            logger.LogInformation(
                "Acknowledged {EventType} (MessageId: {MessageId}, Queue: {Queue}) in {ElapsedMs} ms",
                eventType,
                messageId,
                QueueName,
                Math.Round(sw.Elapsed.TotalMilliseconds, 1));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException or FormatException)
        {
            // Постійний збій (пошкоджений JSON, несумісна версія схеми, невалідні бізнес-дані): одразу в DLQ без повторів
            sw.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.Consumed(QueueName, eventType, success: false);
            metrics.DeadLettered(QueueName, ex.GetType().Name);
            metrics.RecordProcessingDuration(QueueName, success: false, sw.Elapsed.TotalMilliseconds);

            logger.LogError(
                ex,
                "Permanent failure for {EventType} (MessageId: {MessageId}, Queue: {Queue}, RoutingKey: {RoutingKey}, CorrelationId: {CorrelationId}): sending directly to DLQ",
                eventType,
                messageId,
                QueueName,
                ea.RoutingKey,
                correlationId);

            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            // Тимчасовий збій (БД недоступна, таймаут): повтори зі збільшеним x-retry-count, після MaxRetries -> DLQ
            sw.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.Consumed(QueueName, eventType, success: false);
            metrics.RecordProcessingDuration(QueueName, success: false, sw.Elapsed.TotalMilliseconds);

            await RetryOrDeadLetterAsync(ea, bodyCopy, eventType, messageId, correlationId, ex);
        }
    }

    private async Task RetryOrDeadLetterAsync(
        BasicDeliverEventArgs ea,
        byte[] bodyCopy,
        string eventType,
        string messageId,
        string? correlationId,
        Exception ex)
    {
        var headers = ea.BasicProperties.Headers is not null
            ? new Dictionary<string, object?>(ea.BasicProperties.Headers)
            : new Dictionary<string, object?>();

        var attempt = TracePropagation.ExtractRetryCount(headers);

        if (attempt >= MaxRetries)
        {
            metrics.DeadLettered(QueueName, "max_retries_exceeded");
            logger.LogError(
                ex,
                "Message {MessageId} ({EventType}) on queue {Queue} failed after {Attempts}/{MaxRetries} attempts [CorrelationId: {CorrelationId}], routing to DLQ",
                messageId,
                eventType,
                QueueName,
                attempt,
                MaxRetries,
                correlationId);

            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        var nextAttempt = attempt + 1;
        headers["x-retry-count"] = nextAttempt;

        var retryProps = new BasicProperties(ea.BasicProperties)
        {
            Persistent = true,
            Headers = headers
        };

        // Повторна публікація зі збільшеним x-retry-count та підтвердження оригіналу
        await _channel!.BasicPublishAsync(
            exchange: ea.Exchange,
            routingKey: ea.RoutingKey,
            mandatory: false,
            basicProperties: retryProps,
            body: bodyCopy);

        await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

        metrics.Retried(QueueName, eventType);

        logger.LogWarning(
            ex,
            "Message {MessageId} ({EventType}) on queue {Queue} scheduled for retry {Attempt}/{MaxRetries} [CorrelationId: {CorrelationId}]",
            messageId,
            eventType,
            QueueName,
            nextAttempt,
            MaxRetries,
            correlationId);
    }

    private static string ResolveEventType(BasicDeliverEventArgs ea)
    {
        if (!string.IsNullOrWhiteSpace(ea.BasicProperties.Type))
        {
            return ea.BasicProperties.Type;
        }

        var headerType = TracePropagation.ExtractHeaderString(ea.BasicProperties.Headers, "x-event-type");
        if (!string.IsNullOrWhiteSpace(headerType))
        {
            return headerType;
        }

        return ea.RoutingKey switch
        {
            RabbitMqTopology.ProductPriceChangedRoutingKey => nameof(Events.ProductPriceChangedEvent),
            RabbitMqTopology.ProductRenamedRoutingKey => nameof(Events.ProductRenamedEvent),
            RabbitMqTopology.ProductCreatedRoutingKey => nameof(Events.ProductCreatedEvent),
            RabbitMqTopology.ProductDeletedRoutingKey => nameof(Events.ProductDeletedEvent),
            RabbitMqTopology.OrderStatusChangedRoutingKey => nameof(Events.OrderStatusChangedEvent),
            _ => ea.RoutingKey
        };
    }

    private static void ValidateSchemaVersion(IDictionary<string, object?>? headers)
    {
        var versionStr = TracePropagation.ExtractHeaderString(headers, "x-schema-version");
        if (string.IsNullOrWhiteSpace(versionStr))
        {
            return;
        }

        if (!int.TryParse(versionStr, out var version) || version > SupportedSchemaVersion || version < 1)
        {
            throw new NotSupportedException($"Unsupported event schema version '{versionStr}' (max supported: {SupportedSchemaVersion}).");
        }
    }
}
