using System.Diagnostics.Metrics;

namespace Sales.Api.Metrics;

/// <summary>
/// Власні бізнес-метрики сервісу замовлень/угод через OpenTelemetry Meter API (IMeterFactory):
/// - Counter&lt;long&gt; orders.created, orders.confirmed, orders.lost (з низькокардинальними тегами order.status)
/// - Histogram&lt;double&gt; orders.operation.duration (тривалість в мс для успішних і невдалих операцій)
/// - ObservableGauge&lt;int&gt; orders.awaiting_validation (поточний стан активних угод, що очікують підтвердження)
/// Жодних тегів високої кардинальності (UserId, OrderId, Timestamp) не використовується.
/// </summary>
public sealed class OrderMetrics
{
    public const string MeterName = "Orders.Api";

    private readonly Counter<long> _ordersCreated;
    private readonly Counter<long> _ordersConfirmed;
    private readonly Counter<long> _ordersLost;
    private readonly Histogram<double> _operationDuration;
    private int _awaitingValidationCount;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _ordersCreated = meter.CreateCounter<long>(
            "orders.created",
            unit: "{order}",
            description: "Number of created orders/deals");

        _ordersConfirmed = meter.CreateCounter<long>(
            "orders.confirmed",
            unit: "{order}",
            description: "Number of confirmed (won) orders/deals");

        _ordersLost = meter.CreateCounter<long>(
            "orders.lost",
            unit: "{order}",
            description: "Number of lost orders/deals");

        _operationDuration = meter.CreateHistogram<double>(
            "orders.operation.duration",
            unit: "ms",
            description: "Duration of order operations in milliseconds");

        meter.CreateObservableGauge(
            "orders.awaiting_validation",
            () => Volatile.Read(ref _awaitingValidationCount),
            unit: "{order}",
            description: "Orders currently awaiting validation/confirmation");
    }

    public void OrderCreated(string status)
    {
        Interlocked.Increment(ref _awaitingValidationCount);
        _ordersCreated.Add(1, new KeyValuePair<string, object?>("order.status", status));
    }

    public void OrderConfirmed()
    {
        DecrementAwaitingSafely();
        _ordersConfirmed.Add(1, new KeyValuePair<string, object?>("order.status", "Won"));
    }

    public void OrderLost()
    {
        DecrementAwaitingSafely();
        _ordersLost.Add(1, new KeyValuePair<string, object?>("order.status", "Lost"));
    }

    public void RecordDuration(string operation, bool success, double ms) =>
        _operationDuration.Record(
            ms,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));

    private void DecrementAwaitingSafely()
    {
        int current;
        do
        {
            current = Volatile.Read(ref _awaitingValidationCount);
            if (current <= 0)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _awaitingValidationCount, current - 1, current) != current);
    }
}
