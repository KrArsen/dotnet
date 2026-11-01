using System.Diagnostics.Metrics;

namespace Sales.Bll.Caching;

/// <summary>
/// Метрики попадань і промахів дворівневого кешу Sales/Orders (L1 Memory / L2 Redis).
/// </summary>
public sealed class SalesCacheMetrics
{
    public const string MeterName = "Sales.Cache";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;

    public SalesCacheMetrics(IMeterFactory? meterFactory = null)
    {
        var meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName, "1.0.0");
        _hits = meter.CreateCounter<long>("cache.hits", unit: "{hit}", description: "Cache hits by level");
        _misses = meter.CreateCounter<long>("cache.misses", unit: "{miss}", description: "Cache misses by level");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", unit: "{invalidation}", description: "Cache invalidations");
    }

    public void Hit(string level) =>
        _hits.Add(1, new KeyValuePair<string, object?>("cache.level", level));

    public void Miss(string level) =>
        _misses.Add(1, new KeyValuePair<string, object?>("cache.level", level));

    public void Invalidated(string reason) =>
        _invalidations.Add(1, new KeyValuePair<string, object?>("cache.reason", reason));
}
