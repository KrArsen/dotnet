using Pulumi;
using Keycloak = Pulumi.Keycloak;

namespace KeycloakInfra;

/// <summary>
/// Extension-методи для налаштування default scopes, audience protocol mappers та permission mappers у клієнтах Keycloak.
/// </summary>
public static class KeycloakClientExtensions
{
    public static Keycloak.OpenId.AudienceProtocolMapper AddAudienceMapper(
        this Keycloak.OpenId.Client client,
        string name,
        Output<string> realmId,
        string includedClientAudience)
    {
        return new Keycloak.OpenId.AudienceProtocolMapper(name, new Keycloak.OpenId.AudienceProtocolMapperArgs
        {
            RealmId = realmId,
            ClientId = client.Id,
            Name = $"aud-{includedClientAudience}",
            IncludedClientAudience = includedClientAudience,
            AddToIdToken = false,
            AddToAccessToken = true
        });
    }

    public static Keycloak.GenericProtocolMapper AddPermissionsMapper(
        this Keycloak.OpenId.Client client,
        string name,
        Output<string> realmId)
    {
        // Protocol Mapper для передачі claim authorization.permissions у JWT access token
        return new Keycloak.GenericProtocolMapper(name, new Keycloak.GenericProtocolMapperArgs
        {
            RealmId = realmId,
            ClientId = client.Id,
            Name = "permissions-claim-mapper",
            Protocol = "openid-connect",
            ProtocolMapper = "oidc-usermodel-attribute-mapper",
            Config =
            {
                ["user.attribute"] = "permissions",
                ["claim.name"] = "permission",
                ["jsonType.label"] = "String",
                ["multivalued"] = "true",
                ["id.token.claim"] = "false",
                ["access.token.claim"] = "true",
                ["userinfo.token.claim"] = "true"
            }
        });
    }

    public static Keycloak.OpenId.ClientDefaultScopes AttachDefaultScopes(
        this Keycloak.OpenId.Client client,
        string name,
        Output<string> realmId,
        params Input<string>[] scopes)
    {
        var list = new InputList<string> { "openid", "profile", "email", "roles" };
        foreach (var scope in scopes)
        {
            list.Add(scope);
        }

        return new Keycloak.OpenId.ClientDefaultScopes(name, new Keycloak.OpenId.ClientDefaultScopesArgs
        {
            RealmId = realmId,
            ClientId = client.Id,
            DefaultScopes = list
        });
    }
}
