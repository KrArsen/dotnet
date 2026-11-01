using System.Net;
using System.Net.Http.Json;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Typed HttpClient для взаємодії з мікросервісом замовлень/угод (orders-api / sales-api) через Service Discovery.
/// </summary>
public class OrdersClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OrdersClient> _logger;

    public OrdersClient(HttpClient http, ILogger<OrdersClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrderDto?> GetOrderAsync(long id, CancellationToken ct = default)
    {
        _logger.LogInformation("Calling orders-api (sales-api) for order/deal {OrderId}", id);

        using var response = await _http.GetAsync($"/api/deals/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>(cancellationToken: ct);
    }
}
