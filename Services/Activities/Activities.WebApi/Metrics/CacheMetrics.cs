using System.Diagnostics.Metrics;

namespace Activities.WebApi.Metrics;

/// <summary>
/// Метрики попадань і промахів кешу через OpenTelemetry Meter (cache.hits / cache.misses з тегом cache.level = L1/L2).
/// </summary>
public sealed class CacheMetrics
{
    public const string MeterName = "Activities.Cache";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;

    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", unit: "{hit}", description: "Cache hits by level");
        _misses = meter.CreateCounter<long>("cache.misses", unit: "{miss}", description: "Cache misses by level");
    }

    public void Hit(string level) =>
        _hits.Add(1, new KeyValuePair<string, object?>("cache.level", level));

    public void Miss(string level) =>
        _misses.Add(1, new KeyValuePair<string, object?>("cache.level", level));
}
