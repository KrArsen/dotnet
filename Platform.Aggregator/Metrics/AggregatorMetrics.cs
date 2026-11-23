using System.Diagnostics.Metrics;

namespace Platform.Aggregator.Metrics;

/// <summary>
/// Власні бізнес- та операційні метрики Aggregator Service через IMeterFactory (низька кардинальність тегів).
/// </summary>
public sealed class AggregatorMetrics
{
    public const string MeterName = "Platform.Aggregator";

    private readonly Counter<long> _aggregationsTotal;
    private readonly Counter<long> _partialResponses;
    private readonly Histogram<double> _operationDuration;
    private int _inFlightRequests;

    public AggregatorMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _aggregationsTotal = meter.CreateCounter<long>(
            "aggregator.orders.fetched",
            unit: "{request}",
            description: "Total full-order aggregation requests by outcome and cache source");

        _partialResponses = meter.CreateCounter<long>(
            "aggregator.partial_responses",
            unit: "{response}",
            description: "Number of partial responses returned due to downstream failures");

        _operationDuration = meter.CreateHistogram<double>(
            "aggregator.operation.duration",
            unit: "ms",
            description: "Duration of aggregation operations in milliseconds");

        meter.CreateObservableGauge(
            "aggregator.in_flight_requests",
            () => Volatile.Read(ref _inFlightRequests),
            unit: "{request}",
            description: "Current number of active in-flight aggregation requests");
    }

    public void RequestStarted() => Interlocked.Increment(ref _inFlightRequests);
    public void RequestCompleted() => Interlocked.Decrement(ref _inFlightRequests);

    public void RecordAggregation(string outcome, string source, bool isPartial)
    {
        _aggregationsTotal.Add(
            1,
            new KeyValuePair<string, object?>("status", outcome),
            new KeyValuePair<string, object?>("source", source));

        if (isPartial)
        {
            _partialResponses.Add(1, new KeyValuePair<string, object?>("source", source));
        }
    }

    public void RecordDuration(string operation, bool success, double ms) =>
        _operationDuration.Record(
            ms,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
}
