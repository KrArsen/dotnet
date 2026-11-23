using System.Diagnostics.Metrics;

namespace Catalog.Api.Metrics;

/// <summary>
/// Власні бізнес-метрики сервісу Catalog через OpenTelemetry Meter API (IMeterFactory):
/// - Counter&lt;long&gt; products.created, products.updated, products.deleted (низькокардинальні теги)
/// - Histogram&lt;double&gt; catalog.operation.duration (мс, для успішних і невдалих операцій)
/// - ObservableGauge&lt;int&gt; catalog.active_requests (поточна кількість активних запитів до каталогу)
/// </summary>
public sealed class CatalogMetrics
{
    public const string MeterName = "Catalog.Api";

    private readonly Counter<long> _productsCreated;
    private readonly Counter<long> _productsUpdated;
    private readonly Counter<long> _productsDeleted;
    private readonly Histogram<double> _operationDuration;
    private int _activeRequests;

    public CatalogMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _productsCreated = meter.CreateCounter<long>(
            "products.created",
            unit: "{product}",
            description: "Number of created catalog products/services");

        _productsUpdated = meter.CreateCounter<long>(
            "products.updated",
            unit: "{product}",
            description: "Number of catalog product updates (price or stock)");

        _productsDeleted = meter.CreateCounter<long>(
            "products.deleted",
            unit: "{product}",
            description: "Number of soft-deleted catalog products");

        _operationDuration = meter.CreateHistogram<double>(
            "catalog.operation.duration",
            unit: "ms",
            description: "Duration of catalog operations in milliseconds");

        meter.CreateObservableGauge(
            "catalog.active_requests",
            () => Volatile.Read(ref _activeRequests),
            unit: "{request}",
            description: "Current number of active in-flight catalog requests");
    }

    public void RequestStarted() => Interlocked.Increment(ref _activeRequests);
    public void RequestCompleted() => Interlocked.Decrement(ref _activeRequests);

    public void ProductCreated(string itemType) =>
        _productsCreated.Add(1, new KeyValuePair<string, object?>("item.type", itemType));

    public void ProductUpdated(string updateType) =>
        _productsUpdated.Add(1, new KeyValuePair<string, object?>("update.type", updateType));

    public void ProductDeleted() =>
        _productsDeleted.Add(1);

    public void RecordDuration(string operation, bool success, double ms) =>
        _operationDuration.Record(
            ms,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
}
