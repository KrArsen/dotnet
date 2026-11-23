using Pulumi;
using Keycloak = Pulumi.Keycloak;

namespace KeycloakInfra;

/// <summary>
/// Pulumi Stack для декларативного розгортання Keycloak Realm "DevRealmDrivers":
/// - Realm з RegistrationAllowed, ResetPasswordAllowed, RememberMe, themes;
/// - 3 Resource Servers (orders-api, products-api, payments-api) із повним набором scopes (Таблиця 6);
/// - Realm-ролі admin, manager, user, guest, client-level ролі, composite roles та Permission Policies (Таблиця 7);
/// - Clients (orders.postman, orders.swagger з PKCE, orders.bff M2M) з audience і permission mappers;
/// - Тестові користувачі для кожної ролі з temporary паролями й підтвердженим email;
/// - Outputs: RealmUrl, AdminConsoleUrl, ClientIds, ClientSecrets.
/// </summary>
public sealed class KeycloakStack : Stack
{
    public KeycloakStack()
    {
        var config = new Config("keycloak");
        var keycloakUrl = config.Get("url") ?? "http://localhost:8080";

        // 1. Створення Realm DevRealmDrivers
        var realm = new Keycloak.Realm("dev-realm-drivers", new Keycloak.RealmArgs
        {
            RealmName = "DevRealmDrivers",
            DisplayName = "CRM Microservices Platform (DevRealmDrivers)",
            Enabled = true,
            RegistrationAllowed = true,
            ResetPasswordAllowed = true,
            RememberMe = true,
            LoginWithEmailAllowed = true,
            VerifyEmail = false,
            LoginTheme = "keycloak",
            AccountTheme = "keycloak.v2",
            EmailTheme = "keycloak"
        });

        // 2. Realm-ролі: admin, manager, user, guest (Таблиця 7)
        var adminRole = CreateRealmRole("role-admin", realm.Id, "admin", "Full administrative access across all services including payments");
        var managerRole = CreateRealmRole("role-manager", realm.Id, "manager", "Order and catalog management (no delete, no payments)");
        var userRole = CreateRealmRole("role-user", realm.Id, "user", "Standard authenticated customer/user");
        var guestRole = CreateRealmRole("role-guest", realm.Id, "guest", "Read-only catalog guest");

        // 3. Resource Servers із Fine-Grained Authorization Services (ENFORCING)
        var ordersApi = KeycloakClientFactory.CreateResourceServer(
            "rs-orders-api", realm.Id, "orders-api", "Orders Microservice Resource Server", "http://localhost:5001");

        var productsApi = KeycloakClientFactory.CreateResourceServer(
            "rs-products-api", realm.Id, "products-api", "Products/Catalog Resource Server", "http://localhost:5002");

        var paymentsApi = KeycloakClientFactory.CreateResourceServer(
            "rs-payments-api", realm.Id, "payments-api", "Payments Resource Server (Admin Only)", "http://localhost:5003");

        // 4. Scopes для кожного Resource Server (Таблиця 6)
        string[] orderScopes = ["orders:read", "orders:create", "orders:update", "orders:delete", "orders:approve"];
        string[] productScopes = ["products:read", "products:create", "products:update", "products:delete", "products:publish"];
        string[] paymentScopes = ["payments:read", "payments:process", "payments:refund"];

        var orderScopeMap = CreateScopes("orders", realm.Id, ordersApi.ResourceServerId, orderScopes);
        var productScopeMap = CreateScopes("products", realm.Id, productsApi.ResourceServerId, productScopes);
        var paymentScopeMap = CreateScopes("payments", realm.Id, paymentsApi.ResourceServerId, paymentScopes);

        // 5. Protected Resources + Role Policies + Scope Permissions за матрицею Таблиці 7:
        //    - admin: ALL permissions on orders-api, products-api, payments-api
        //    - manager: orders(read, update, approve), products(read, update, publish), payments(NONE)
        //    - user: orders(read, create), products(read), payments(NONE)
        //    - guest: products(read), orders(NONE), payments(NONE)
        ConfigureResourceServerPermissions(
            "orders", "/orders/*", realm.Id, ordersApi.ResourceServerId, orderScopeMap,
            new Dictionary<string, (Keycloak.Role Role, string[] Scopes)>
            {
                ["admin"] = (adminRole, orderScopes),
                ["manager"] = (managerRole, ["orders:read", "orders:update", "orders:approve"]),
                ["user"] = (userRole, ["orders:read", "orders:create"])
            });

        ConfigureResourceServerPermissions(
            "products", "/products/*", realm.Id, productsApi.ResourceServerId, productScopeMap,
            new Dictionary<string, (Keycloak.Role Role, string[] Scopes)>
            {
                ["admin"] = (adminRole, productScopes),
                ["manager"] = (managerRole, ["products:read", "products:update", "products:publish"]),
                ["user"] = (userRole, ["products:read"]),
                ["guest"] = (guestRole, ["products:read"])
            });

        // ВАЖЛИВО: payments-api видає permissions ВИКЛЮЧНО ролі admin (manager/user/guest не мають доступу)
        ConfigureResourceServerPermissions(
            "payments", "/payments/*", realm.Id, paymentsApi.ResourceServerId, paymentScopeMap,
            new Dictionary<string, (Keycloak.Role Role, string[] Scopes)>
            {
                ["admin"] = (adminRole, paymentScopes)
            });

        // 6. Client-level ролі для service accounts та composite role
        var ordersServiceAccountRole = new Keycloak.Role("client-role-orders-reader", new Keycloak.RoleArgs
        {
            RealmId = realm.Id,
            ClientId = ordersApi.Id,
            Name = "orders-service-reader",
            Description = "Client-level service account role for reading orders"
        });

        var productsServiceAccountRole = new Keycloak.Role("client-role-products-reader", new Keycloak.RoleArgs
        {
            RealmId = realm.Id,
            ClientId = productsApi.Id,
            Name = "products-service-reader",
            Description = "Client-level service account role for reading products"
        });

        _ = new Keycloak.Role("composite-aggregator-role", new Keycloak.RoleArgs
        {
            RealmId = realm.Id,
            Name = "aggregator-composite",
            Description = "Composite role grouping service-to-service read permissions",
            CompositeRoles = { ordersServiceAccountRole.Id, productsServiceAccountRole.Id }
        });

        // 7. Client Scopes для зручного групування дозволів
        var ordersMgmtScope = new Keycloak.OpenId.ClientScope("scope-orders-mgmt", new Keycloak.OpenId.ClientScopeArgs
        {
            RealmId = realm.Id,
            Name = "orders_mgmt",
            Description = "Orders management scope"
        });

        var productsMgmtScope = new Keycloak.OpenId.ClientScope("scope-products-mgmt", new Keycloak.OpenId.ClientScopeArgs
        {
            RealmId = realm.Id,
            Name = "products_mgmt",
            Description = "Products management scope"
        });

        // 8. Clients через Factory Pattern + Extension Methods
        var postmanClient = KeycloakClientFactory.CreateAuthorizationCodeClient(
            "client-orders-postman",
            realm.Id,
            "orders.postman",
            "Postman OAuth2 Client",
            "https://oauth.pstmn.io/v1/callback");

        postmanClient.AddAudienceMapper("postman-aud-orders", realm.Id, "orders-api");
        postmanClient.AddAudienceMapper("postman-aud-products", realm.Id, "products-api");
        postmanClient.AddAudienceMapper("postman-aud-payments", realm.Id, "payments-api");
        postmanClient.AddPermissionsMapper("postman-permissions-mapper", realm.Id);
        postmanClient.AttachDefaultScopes("postman-default-scopes", realm.Id, ordersMgmtScope.Name, productsMgmtScope.Name);

        var swaggerClient = KeycloakClientFactory.CreatePublicPkceClient(
            "client-orders-swagger",
            realm.Id,
            "orders.swagger",
            "Swagger UI Public Client (PKCE)",
            "http://localhost:5000/swagger/oauth2-redirect.html",
            "https://localhost:5001/swagger/oauth2-redirect.html");

        swaggerClient.AddAudienceMapper("swagger-aud-orders", realm.Id, "orders-api");
        swaggerClient.AddAudienceMapper("swagger-aud-products", realm.Id, "products-api");
        swaggerClient.AddPermissionsMapper("swagger-permissions-mapper", realm.Id);
        swaggerClient.AttachDefaultScopes("swagger-default-scopes", realm.Id, ordersMgmtScope.Name, productsMgmtScope.Name);

        var bffAggregatorClient = KeycloakClientFactory.CreateClientCredentialsClient(
            "client-orders-bff",
            realm.Id,
            "orders.bff",
            "Platform Aggregator / BFF Client Credentials");

        bffAggregatorClient.AddAudienceMapper("bff-aud-orders", realm.Id, "orders-api");
        bffAggregatorClient.AddAudienceMapper("bff-aud-products", realm.Id, "products-api");
        bffAggregatorClient.AddPermissionsMapper("bff-permissions-mapper", realm.Id);

        _ = new Keycloak.OpenId.ClientServiceAccountRole("bff-sa-orders-reader", new Keycloak.OpenId.ClientServiceAccountRoleArgs
        {
            RealmId = realm.Id,
            ServiceAccountUserId = bffAggregatorClient.ServiceAccountUserId,
            ClientId = ordersApi.Id,
            Role = ordersServiceAccountRole.Name
        });

        _ = new Keycloak.OpenId.ClientServiceAccountRole("bff-sa-products-reader", new Keycloak.OpenId.ClientServiceAccountRoleArgs
        {
            RealmId = realm.Id,
            ServiceAccountUserId = bffAggregatorClient.ServiceAccountUserId,
            ClientId = productsApi.Id,
            Role = productsServiceAccountRole.Name
        });

        // 9. Тестові користувачі для кожної ролі з тимчасовими паролями, EmailVerified = true та UserRoles
        CreateTestUser("user-alice", realm.Id, "alice.admin", "alice@studio.ua", "Alice", "Admin", "Admin123!", adminRole.Id);
        CreateTestUser("user-john", realm.Id, "john.manager", "john.manager@studio.ua", "John", "Manager", "Manager123!", managerRole.Id);
        CreateTestUser("user-bob", realm.Id, "bob.user", "bob@studio.ua", "Bob", "User", "User123!", userRole.Id);
        CreateTestUser("user-guest", realm.Id, "guest.viewer", "guest@studio.ua", "Guest", "Viewer", "Guest123!", guestRole.Id);

        // 10. Outputs стеку (pulumi stack output)
        RealmUrl = realm.RealmName.Apply(r => $"{keycloakUrl.TrimEnd('/')}/realms/{r}");
        AdminConsoleUrl = Output.Create($"{keycloakUrl.TrimEnd('/')}/admin/DevRealmDrivers/console");
        ClientIds = Output.Tuple(postmanClient.ClientId, swaggerClient.ClientId, bffAggregatorClient.ClientId, ordersApi.ClientId, productsApi.ClientId, paymentsApi.ClientId)
            .Apply(t => new Dictionary<string, string>
            {
                ["postman"] = t.Item1,
                ["swagger"] = t.Item2,
                ["bff"] = t.Item3,
                ["ordersApi"] = t.Item4,
                ["productsApi"] = t.Item5,
                ["paymentsApi"] = t.Item6
            });
        ClientSecrets = Output.Tuple(postmanClient.ClientSecret, bffAggregatorClient.ClientSecret)
            .Apply(t => new Dictionary<string, string>
            {
                ["postman"] = t.Item1,
                ["bff"] = t.Item2
            });
    }

    [Output] public Output<string> RealmUrl { get; set; }
    [Output] public Output<string> AdminConsoleUrl { get; set; }
    [Output] public Output<Dictionary<string, string>> ClientIds { get; set; }
    [Output] public Output<Dictionary<string, string>> ClientSecrets { get; set; }

    private static Keycloak.Role CreateRealmRole(string resName, Output<string> realmId, string name, string description) =>
        new(resName, new Keycloak.RoleArgs
        {
            RealmId = realmId,
            Name = name,
            Description = description
        });

    private static Dictionary<string, Keycloak.OpenId.ClientAuthorizationScope> CreateScopes(
        string prefix,
        Output<string> realmId,
        Output<string> resourceServerId,
        IEnumerable<string> scopeNames)
    {
        var map = new Dictionary<string, Keycloak.OpenId.ClientAuthorizationScope>();
        foreach (var scopeName in scopeNames)
        {
            var safe = scopeName.Replace(":", "-");
            map[scopeName] = new Keycloak.OpenId.ClientAuthorizationScope(
                $"{prefix}-scope-{safe}",
                new Keycloak.OpenId.ClientAuthorizationScopeArgs
                {
                    RealmId = realmId,
                    ResourceServerId = resourceServerId,
                    Name = scopeName,
                    DisplayName = scopeName
                });
        }
        return map;
    }

    private static void ConfigureResourceServerPermissions(
        string prefix,
        string uriPattern,
        Output<string> realmId,
        Output<string> resourceServerId,
        Dictionary<string, Keycloak.OpenId.ClientAuthorizationScope> scopeMap,
        Dictionary<string, (Keycloak.Role Role, string[] Scopes)> roleMatrix)
    {
        var allScopeNames = new InputList<string>();
        foreach (var scope in scopeMap.Values)
        {
            allScopeNames.Add(scope.Name);
        }

        var resource = new Keycloak.OpenId.ClientAuthorizationResource(
            $"{prefix}-resource",
            new Keycloak.OpenId.ClientAuthorizationResourceArgs
            {
                RealmId = realmId,
                ResourceServerId = resourceServerId,
                Name = $"{prefix}-resource",
                DisplayName = $"{prefix} API Resource",
                Uris = { uriPattern },
                Scopes = allScopeNames
            });

        foreach (var (roleName, (role, allowedScopes)) in roleMatrix)
        {
            var policy = new Keycloak.OpenId.ClientRolePolicy(
                $"{prefix}-policy-{roleName}",
                new Keycloak.OpenId.ClientRolePolicyArgs
                {
                    RealmId = realmId,
                    ResourceServerId = resourceServerId,
                    Name = $"{prefix}-{roleName}-policy",
                    Type = "role",
                    DecisionStrategy = "UNANIMOUS",
                    Logic = "POSITIVE",
                    Roles =
                    {
                        new Keycloak.OpenId.Inputs.ClientRolePolicyRoleArgs
                        {
                            Id = role.Id,
                            Required = true
                        }
                    }
                });

            var scopeInputList = new InputList<string>();
            foreach (var scopeName in allowedScopes)
            {
                scopeInputList.Add(scopeMap[scopeName].Id);
            }

            _ = new Keycloak.OpenId.ClientAuthorizationPermission(
                $"{prefix}-perm-{roleName}",
                new Keycloak.OpenId.ClientAuthorizationPermissionArgs
                {
                    RealmId = realmId,
                    ResourceServerId = resourceServerId,
                    Name = $"{prefix}-{roleName}-permission",
                    DecisionStrategy = "AFFIRMATIVE",
                    Type = "scope",
                    Resources = { resource.Id },
                    Scopes = scopeInputList,
                    Policies = { policy.Id }
                });
        }
    }

    private static void CreateTestUser(
        string resourceName,
        Output<string> realmId,
        string username,
        string email,
        string firstName,
        string lastName,
        string tempPassword,
        Output<string> roleId)
    {
        var user = new Keycloak.User(resourceName, new Keycloak.UserArgs
        {
            RealmId = realmId,
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Enabled = true,
            EmailVerified = true,
            InitialPassword = new Keycloak.Inputs.UserInitialPasswordArgs
            {
                Value = tempPassword,
                Temporary = true
            }
        });

        _ = new Keycloak.UserRoles($"{resourceName}-roles", new Keycloak.UserRolesArgs
        {
            RealmId = realmId,
            UserId = user.Id,
            RoleIds = { roleId }
        });
    }
}
