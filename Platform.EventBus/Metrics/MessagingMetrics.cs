using System.Diagnostics.Metrics;

namespace Platform.EventBus.Metrics;

/// <summary>
/// Метрики асинхронної подієвої взаємодії через OpenTelemetry Meter API ("Platform.Messaging"):
/// лічильники публікацій, споживання, повторів, дублікатів, повідомлень у DLQ та гістограма тривалості обробки.
/// </summary>
public sealed class MessagingMetrics
{
    public const string MeterName = "Platform.Messaging";

    private readonly Counter<long> _published;
    private readonly Counter<long> _consumed;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _deadLettered;
    private readonly Counter<long> _duplicatesSkipped;
    private readonly Histogram<double> _processingDuration;
    private long _dlqRoutedCount;

    public MessagingMetrics(IMeterFactory? meterFactory = null)
    {
        var meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName, "1.0.0");

        _published = meter.CreateCounter<long>(
            "messaging.events.published",
            unit: "{message}",
            description: "Number of published integration events");

        _consumed = meter.CreateCounter<long>(
            "messaging.events.consumed",
            unit: "{message}",
            description: "Number of processed integration events by queue and status");

        _retries = meter.CreateCounter<long>(
            "messaging.events.retries",
            unit: "{retry}",
            description: "Number of message retry publications (x-retry-count)");

        _deadLettered = meter.CreateCounter<long>(
            "messaging.events.dead_lettered",
            unit: "{message}",
            description: "Number of messages routed to Dead Letter Queue");

        _duplicatesSkipped = meter.CreateCounter<long>(
            "messaging.events.duplicates_skipped",
            unit: "{message}",
            description: "Number of duplicate events skipped by idempotency check");

        _processingDuration = meter.CreateHistogram<double>(
            "messaging.processing.duration",
            unit: "ms",
            description: "Duration of consumer message processing in milliseconds");

        meter.CreateObservableGauge(
            "messaging.dlq.messages_routed",
            () => Interlocked.Read(ref _dlqRoutedCount),
            unit: "{message}",
            description: "Cumulative count of messages routed to DLQ in current process");
    }

    public void Published(string exchange, string eventType) =>
        _published.Add(
            1,
            new KeyValuePair<string, object?>("exchange", exchange),
            new KeyValuePair<string, object?>("event.type", eventType));

    public void Consumed(string queue, string eventType, bool success) =>
        _consumed.Add(
            1,
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("event.type", eventType),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));

    public void Retried(string queue, string eventType) =>
        _retries.Add(
            1,
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("event.type", eventType));

    public void DeadLettered(string queue, string reason)
    {
        Interlocked.Increment(ref _dlqRoutedCount);
        _deadLettered.Add(
            1,
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void DuplicateSkipped(string queue, string eventType) =>
        _duplicatesSkipped.Add(
            1,
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("event.type", eventType));

    public void RecordProcessingDuration(string queue, bool success, double ms) =>
        _processingDuration.Record(
            ms,
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
}
