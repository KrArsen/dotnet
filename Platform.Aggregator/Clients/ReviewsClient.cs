using System.Net.Http.Json;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Typed HttpClient для взаємодії з мікросервісом відгуків (reviews-api / activities-api) через Service Discovery.
/// </summary>
public class ReviewsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ReviewsClient> _logger;

    public ReviewsClient(HttpClient http, ILogger<ReviewsClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ReviewDto>> GetForProductAsync(long productId, CancellationToken ct = default)
    {
        _logger.LogInformation("Calling reviews-api for product {ProductId}", productId);

        var paged = await _http.GetFromJsonAsync<PagedResponse<ReviewDto>>(
            $"/api/reviews?productId={productId}&page=1&pageSize=50",
            ct);

        return paged?.Items ?? [];
    }

    public async Task<IReadOnlyList<ReviewDto>> GetForProductsAsync(
        IReadOnlyCollection<long> productIds,
        CancellationToken ct = default)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        _logger.LogInformation(
            "Calling reviews-api for {ProductCount} products: [{ProductIds}]",
            productIds.Count,
            string.Join(", ", productIds));

        var tasks = productIds
            .Distinct()
            .Select(id => GetForProductAsync(id, ct));

        var results = await Task.WhenAll(tasks);

        return results
            .SelectMany(r => r)
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .ToList();
    }
}
