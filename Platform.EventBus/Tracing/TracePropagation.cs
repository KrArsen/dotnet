using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus.Tracing;

/// <summary>
/// Передача W3C Trace Context (traceparent / tracestate) та X-Correlation-Id через заголовки RabbitMQ.
/// Забезпечує наскрізний trace у Aspire Dashboard: HTTP-запит -> Publish (Producer) -> Consume (Consumer) -> БД.
/// </summary>
public static class TracePropagation
{
    public const string ActivitySourceName = "Platform.Messaging";
    public const string CorrelationIdHeader = "x-correlation-id";

    public static readonly ActivitySource Source = new(ActivitySourceName);
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public static Activity? StartProducerActivity(
        string exchange,
        string routingKey,
        string eventType,
        Guid eventId)
    {
        var activity = Source.StartActivity($"{exchange} publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("messaging.destination.name", exchange);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity?.SetTag("messaging.message.id", eventId.ToString());
        activity?.SetTag("messaging.event.type", eventType);
        return activity;
    }

    public static void Inject(BasicProperties props)
    {
        if (Activity.Current is not { } activity)
        {
            return;
        }

        props.Headers ??= new Dictionary<string, object?>();
        Propagator.Inject(
            new PropagationContext(activity.Context, Baggage.Current),
            props.Headers,
            static (headers, key, value) => headers[key] = value);
    }

    public static Activity? StartConsumerActivity(BasicDeliverEventArgs ea, string queue)
    {
        var parent = Propagator.Extract(
            default,
            ea.BasicProperties.Headers,
            static (headers, key) =>
            {
                if (headers is null || !headers.TryGetValue(key, out var raw) || raw is null)
                {
                    return [];
                }

                return raw switch
                {
                    byte[] bytes => [Encoding.UTF8.GetString(bytes)],
                    string text => [text],
                    _ => [raw.ToString() ?? string.Empty]
                };
            });

        Baggage.Current = parent.Baggage;

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", ea.RoutingKey);
        activity?.SetTag("messaging.message.id", ea.BasicProperties.MessageId);
        activity?.SetTag("messaging.event.type", ea.BasicProperties.Type);
        return activity;
    }

    public static string? ExtractHeaderString(IDictionary<string, object?>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => raw.ToString()
        };
    }

    public static int ExtractRetryCount(IDictionary<string, object?>? headers, string key = "x-retry-count")
    {
        if (headers is null || !headers.TryGetValue(key, out var raw) || raw is null)
        {
            return 0;
        }

        return raw switch
        {
            int i => i,
            long l => (int)l,
            byte b => b,
            short s => s,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            string str when int.TryParse(str, out var parsed) => parsed,
            _ => Convert.ToInt32(raw)
        };
    }
}
