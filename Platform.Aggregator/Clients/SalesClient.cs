using System.Net;
using System.Net.Http.Json;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Typed HttpClient для взаємодії з мікросервісом Sales (sales-api) через Service Discovery.
/// </summary>
public class SalesClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SalesClient> _logger;

    public SalesClient(HttpClient http, ILogger<SalesClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrderDto?> GetDealAsync(long id, CancellationToken ct = default)
    {
        _logger.LogInformation("Calling sales-api for deal {DealId}", id);

        using var response = await _http.GetAsync($"/api/deals/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>(cancellationToken: ct);
    }

    public async Task<ClientSummaryDto?> GetClientAsync(long clientId, CancellationToken ct = default)
    {
        _logger.LogInformation("Calling sales-api for client {ClientId}", clientId);

        using var response = await _http.GetAsync($"/api/clients/{clientId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ClientSummaryDto>(cancellationToken: ct);
    }
}
