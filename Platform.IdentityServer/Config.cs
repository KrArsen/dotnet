using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Platform.IdentityServer;

/// <summary>
/// Конфігурація OAuth 2.0 / OpenID Connect для Duende IdentityServer:
/// IdentityResources, ApiScopes, ApiResources та три клієнти (postman, aggregator_service, swagger з PKCE).
/// </summary>
public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email()
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope("orders_api", "Orders Microservice API"),
        new ApiScope("orders.read", "Read orders and deals"),
        new ApiScope("orders.write", "Create and modify orders and deals"),
        new ApiScope("products_api", "Products / Catalog Microservice API"),
        new ApiScope("products.read", "Read catalog products"),
        new ApiScope("products.write", "Modify catalog products"),
        new ApiScope("payments_api", "Payments Microservice API"),
        new ApiScope("payments.read", "Read payment transactions"),
        new ApiScope("payments.write", "Process and refund payments")
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource("orders_api", "Orders Microservice")
        {
            Scopes = { "orders_api", "orders.read", "orders.write" },
            UserClaims = { "role", "name", "email" }
        },
        new ApiResource("products_api", "Products / Catalog Microservice")
        {
            Scopes = { "products_api", "products.read", "products.write" },
            UserClaims = { "role", "name", "email" }
        },
        new ApiResource("payments_api", "Payments Microservice")
        {
            Scopes = { "payments_api", "payments.read", "payments.write" },
            UserClaims = { "role", "name", "email" }
        }
    ];

    public static IEnumerable<Client> Clients =>
    [
        // 1. Postman — Authorization Code Flow + Offline Access (Refresh Tokens)
        new Client
        {
            ClientId = "postman",
            ClientName = "Postman OAuth2 Client",
            AllowedGrantTypes = GrantTypes.Code,
            ClientSecrets = { new Secret("secret".Sha256()) },
            RedirectUris = { "https://oauth.pstmn.io/v1/callback" },
            PostLogoutRedirectUris = { "https://oauth.pstmn.io/v1/callback" },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email,
                "orders_api", "orders.read", "orders.write",
                "products_api", "products.read", "products.write",
                "payments_api", "payments.read", "payments.write"
            },
            AllowOfflineAccess = true
        },

        // 2. Aggregator — Client Credentials Flow (Machine-to-Machine без user context / sub)
        new Client
        {
            ClientId = "aggregator_service",
            ClientName = "Platform Aggregator M2M Service",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("aggregator_secret".Sha256()) },
            AllowedScopes =
            {
                "orders_api", "orders.read",
                "products_api", "products.read",
                "payments_api", "payments.read"
            }
        },

        // 3. Swagger UI — Public Client із обов'язковим PKCE без ClientSecret
        new Client
        {
            ClientId = "swagger",
            ClientName = "Swagger UI Public Client (PKCE)",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris =
            {
                "http://localhost:5000/swagger/oauth2-redirect.html",
                "https://localhost:5001/swagger/oauth2-redirect.html"
            },
            AllowedCorsOrigins =
            {
                "http://localhost:5000",
                "https://localhost:5001"
            },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "orders_api", "orders.read",
                "products_api", "products.read"
            }
        }
    ];
}
