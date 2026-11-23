using System.Net.Http.Headers;
using System.Text.Json;

namespace Platform.Aggregator.Auth;

/// <summary>
/// OAuth 2.0 Client Credentials Flow (Machine-to-Machine) DelegatingHandler для Platform.Aggregator:
/// - якщо вхідний HTTP-запит уже має Bearer-токен користувача — прокидає його далі;
/// - інакше запитує M2M access token у Platform.IdentityServer (/connect/token, grant_type=client_credentials)
///   для клієнта "aggregator_service" (без sub / user context) та кешує його до закінчення терміну дії.
/// </summary>
public sealed class ClientCredentialsTokenHandler(
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ClientCredentialsTokenHandler> logger) : DelegatingHandler
{
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static string? _cachedToken;
    private static DateTime _tokenExpiresAtUtc = DateTime.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 1. Якщо є вхідний Bearer-токен клієнта — передаємо його в downstream сервіси
        var incomingAuth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(incomingAuth) &&
            incomingAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(incomingAuth);
            return await base.SendAsync(request, cancellationToken);
        }

        // 2. Інакше отримуємо service-to-service токен через Client Credentials Flow у Duende IdentityServer
        var m2mToken = await GetClientCredentialsTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(m2mToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", m2mToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string?> GetClientCredentialsTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTime.UtcNow < _tokenExpiresAtUtc)
        {
            return _cachedToken;
        }

        await TokenLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTime.UtcNow < _tokenExpiresAtUtc)
            {
                return _cachedToken;
            }

            var authority = configuration["IdentityServer:Authority"]
                            ?? configuration["services:identityserver:http:0"]
                            ?? "http://localhost:5005";

            var clientId = configuration["IdentityServer:ClientId"] ?? "aggregator_service";
            var clientSecret = configuration["IdentityServer:ClientSecret"] ?? "aggregator_secret";
            var scope = configuration["IdentityServer:Scope"] ?? "orders_api orders.read products_api products.read";

            using var client = httpClientFactory.CreateClient("identityserver-token");
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = scope
            });

            var response = await client.PostAsync($"{authority.TrimEnd('/')}/connect/token", form, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Client Credentials token request to {Authority}/connect/token returned {StatusCode}", authority, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 300;

            _cachedToken = accessToken;
            _tokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(expiresIn - 30, 30));

            logger.LogInformation("Acquired OAuth 2.0 Client Credentials token for {ClientId} (expires in {ExpiresIn}s)", clientId, expiresIn);
            return _cachedToken;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to acquire Client Credentials token from IdentityServer");
            return null;
        }
        finally
        {
            TokenLock.Release();
        }
    }
}
