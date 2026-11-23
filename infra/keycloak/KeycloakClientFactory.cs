using Pulumi;
using Keycloak = Pulumi.Keycloak;

namespace KeycloakInfra;

/// <summary>
/// Factory Pattern для створення Keycloak OIDC клієнтів і Resource Servers:
/// - Resource Server (Confidential + Fine-Grained Authorization Services ENFORCING);
/// - Authorization Code Flow Client (Postman);
/// - Public PKCE Client (Swagger UI);
/// - Client Credentials M2M Client (BFF / Aggregator).
/// </summary>
public static class KeycloakClientFactory
{
    public static Keycloak.OpenId.Client CreateResourceServer(
        string resourceName,
        Output<string> realmId,
        string clientId,
        string displayName,
        string rootUrl)
    {
        return new Keycloak.OpenId.Client(resourceName, new Keycloak.OpenId.ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = displayName,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            ServiceAccountsEnabled = true,
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            RootUrl = rootUrl,
            Authorization = new Keycloak.OpenId.Inputs.ClientAuthorizationArgs
            {
                PolicyEnforcementMode = "ENFORCING",
                DecisionStrategy = "AFFIRMATIVE",
                AllowRemoteResourceManagement = true,
                KeepDefaults = false
            }
        });
    }

    public static Keycloak.OpenId.Client CreateAuthorizationCodeClient(
        string resourceName,
        Output<string> realmId,
        string clientId,
        string displayName,
        params string[] redirectUris)
    {
        var redirects = new InputList<string>();
        foreach (var uri in redirectUris)
        {
            redirects.Add(uri);
        }

        return new Keycloak.OpenId.Client(resourceName, new Keycloak.OpenId.ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = displayName,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = true,
            ServiceAccountsEnabled = false,
            ValidRedirectUris = redirects,
            WebOrigins = { "+" }
        });
    }

    public static Keycloak.OpenId.Client CreatePublicPkceClient(
        string resourceName,
        Output<string> realmId,
        string clientId,
        string displayName,
        params string[] redirectUris)
    {
        var redirects = new InputList<string>();
        foreach (var uri in redirectUris)
        {
            redirects.Add(uri);
        }

        return new Keycloak.OpenId.Client(resourceName, new Keycloak.OpenId.ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = displayName,
            Enabled = true,
            AccessType = "PUBLIC",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = false,
            PkceCodeChallengeMethod = "S256",
            ValidRedirectUris = redirects,
            WebOrigins = { "+" }
        });
    }

    public static Keycloak.OpenId.Client CreateClientCredentialsClient(
        string resourceName,
        Output<string> realmId,
        string clientId,
        string displayName)
    {
        return new Keycloak.OpenId.Client(resourceName, new Keycloak.OpenId.ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = displayName,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            ServiceAccountsEnabled = true,
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false
        });
    }
}
