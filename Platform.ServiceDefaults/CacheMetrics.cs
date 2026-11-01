using System.Diagnostics.Metrics;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Власні метрики ефективності дворівневого кешування (L1 Memory / L2 Redis) для OpenTelemetry та Aspire Dashboard.
/// Відстежує лічильники cache.hits та cache.misses з тегом cache.level (L1 / L2),
/// кількість інвалідацій (cache.invalidations) та тривалість операцій з кешем (cache.operation.duration).
/// </summary>
public class CacheMetrics
{
    public const string MeterName = "Catalog.Cache";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;
    private readonly Histogram<double> _durationMs;

    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>(
            "cache.hits",
            unit: "{hit}",
            description: "Кількість попадань у кеш за рівнями (L1 Memory / L2 Redis)");

        _misses = meter.CreateCounter<long>(
            "cache.misses",
            unit: "{miss}",
            description: "Кількість промахів кешу за рівнями (L1 Memory / L2 Redis)");

        _invalidations = meter.CreateCounter<long>(
            "cache.invalidations",
            unit: "{invalidation}",
            description: "Кількість синхронних інвалідацій записів кешу");

        _durationMs = meter.CreateHistogram<double>(
            "cache.operation.duration",
            unit: "ms",
            description: "Тривалість операцій читання/запису кешу в мілісекундах");
    }

    public void Hit(string level) =>
        _hits.Add(1, new KeyValuePair<string, object?>("cache.level", level));

    public void Miss(string level) =>
        _misses.Add(1, new KeyValuePair<string, object?>("cache.level", level));

    public void Invalidated(string key, string reason) =>
        _invalidations.Add(1,
            new KeyValuePair<string, object?>("cache.key", key),
            new KeyValuePair<string, object?>("cache.reason", reason));

    public void RecordDuration(double elapsedMs, string level, string operation) =>
        _durationMs.Record(elapsedMs,
            new KeyValuePair<string, object?>("cache.level", level),
            new KeyValuePair<string, object?>("cache.operation", operation));
}
