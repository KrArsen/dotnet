using System.Net.Http.Json;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Typed HttpClient для взаємодії з мікросервісом активностей і комунікацій (activities-api) через Service Discovery.
/// </summary>
public class ActivitiesClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ActivitiesClient> _logger;

    public ActivitiesClient(HttpClient http, ILogger<ActivitiesClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ActivityDto>> GetByClientAsync(long clientId, CancellationToken ct = default)
    {
        _logger.LogInformation("Calling activities-api for client {ClientId}", clientId);

        var paged = await _http.GetFromJsonAsync<PagedResponse<ActivityDto>>(
            $"/api/activities?clientId={clientId}&page=1&pageSize=50",
            ct);

        return paged?.Items ?? [];
    }
}
