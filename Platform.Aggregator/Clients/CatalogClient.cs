using System.Net;
using System.Net.Http.Json;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Typed HttpClient для взаємодії з мікросервісом каталогу (catalog-api) через Service Discovery.
/// </summary>
public class CatalogClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CatalogClient> _logger;

    public CatalogClient(HttpClient http, ILogger<CatalogClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProductDto?> GetProductAsync(long productId, CancellationToken ct = default)
    {
        _logger.LogInformation("Calling catalog-api for product {ProductId}", productId);

        using var response = await _http.GetAsync($"/api/products/{productId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        IReadOnlyCollection<long> productIds,
        CancellationToken ct = default)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        _logger.LogInformation(
            "Calling catalog-api for {ProductCount} products: [{ProductIds}]",
            productIds.Count,
            string.Join(", ", productIds));

        var tasks = productIds
            .Distinct()
            .Select(id => GetProductAsync(id, ct));

        var results = await Task.WhenAll(tasks);

        return results
            .Where(p => p is not null && productIds.Contains(p.Id))
            .Select(p => p!)
            .ToList();
    }
}
