using System.Diagnostics.Metrics;

namespace Catalog.Bll.Caching;

/// <summary>
/// Метрики попадань і промахів дворівневого кешу (L1 Memory / L2 Redis) через OpenTelemetry Meter.
/// </summary>
public sealed class CatalogCacheMetrics
{
    public const string MeterName = "Catalog.Cache";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;

    public CatalogCacheMetrics(IMeterFactory? meterFactory = null)
    {
        var meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName, "1.0.0");
        _hits = meter.CreateCounter<long>(
            "cache.hits",
            unit: "{hit}",
            description: "Кількість попадань у кеш за рівнем (L1/L2)");
        _misses = meter.CreateCounter<long>(
            "cache.misses",
            unit: "{miss}",
            description: "Кількість промахів кешу за рівнем (L1/L2)");
        _invalidations = meter.CreateCounter<long>(
            "cache.invalidations",
            unit: "{invalidation}",
            description: "Кількість операцій інвалідації кешу");
    }

    public void Hit(string level, string prefix = "catalog") =>
        _hits.Add(
            1,
            new KeyValuePair<string, object?>("cache.level", level),
            new KeyValuePair<string, object?>("cache.prefix", prefix));

    public void Miss(string level, string prefix = "catalog") =>
        _misses.Add(
            1,
            new KeyValuePair<string, object?>("cache.level", level),
            new KeyValuePair<string, object?>("cache.prefix", prefix));

    public void Invalidated(string reason, string prefix = "catalog") =>
        _invalidations.Add(
            1,
            new KeyValuePair<string, object?>("cache.reason", reason),
            new KeyValuePair<string, object?>("cache.prefix", prefix));
}
