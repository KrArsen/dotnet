using System.Diagnostics.Metrics;

namespace Activities.WebApi.Metrics;

/// <summary>
/// Власні бізнес-метрики сервісу відгуків та активностей (Reviews/Activities) через IMeterFactory:
/// - Counter&lt;long&gt; reviews.created, activities.created (низькокардинальні теги)
/// - Histogram&lt;double&gt; reviews.operation.duration (мс, для успішних і невдалих операцій)
/// - ObservableGauge&lt;int&gt; reviews.active_requests (кількість активних запитів)
/// </summary>
public sealed class ReviewMetrics
{
    public const string MeterName = "Reviews.Api";

    private readonly Counter<long> _reviewsCreated;
    private readonly Counter<long> _activitiesCreated;
    private readonly Histogram<double> _operationDuration;
    private int _activeRequests;

    public ReviewMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _reviewsCreated = meter.CreateCounter<long>(
            "reviews.created",
            unit: "{review}",
            description: "Number of created product/client reviews");

        _activitiesCreated = meter.CreateCounter<long>(
            "activities.created",
            unit: "{activity}",
            description: "Number of created CRM activities by type");

        _operationDuration = meter.CreateHistogram<double>(
            "reviews.operation.duration",
            unit: "ms",
            description: "Duration of review/activity operations in milliseconds");

        meter.CreateObservableGauge(
            "reviews.active_requests",
            () => Volatile.Read(ref _activeRequests),
            unit: "{request}",
            description: "Current number of active in-flight requests to reviews/activities API");
    }

    public void RequestStarted() => Interlocked.Increment(ref _activeRequests);
    public void RequestCompleted() => Interlocked.Decrement(ref _activeRequests);

    public void ReviewCreated(int rating)
    {
        var bucket = rating switch
        {
            >= 4 => "positive",
            3 => "neutral",
            _ => "negative"
        };
        _reviewsCreated.Add(1, new KeyValuePair<string, object?>("rating.bucket", bucket));
    }

    public void ActivityCreated(string type) =>
        _activitiesCreated.Add(1, new KeyValuePair<string, object?>("activity.type", string.IsNullOrWhiteSpace(type) ? "note" : type));

    public void RecordDuration(string operation, bool success, double ms) =>
        _operationDuration.Record(
            ms,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
}
