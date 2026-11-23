using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Вимога конкретного гранульованого permission (наприклад, "orders:create", "payments:process").
/// </summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission ?? throw new ArgumentNullException(nameof(permission));
}

/// <summary>
/// Атрибут [RequirePermission("orders:create")] для локальної перевірки дозволів на кожному endpoint мікросервісу.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public string Permission { get; }

    public RequirePermissionAttribute(string permission) : base(policy: permission)
    {
        Permission = permission;
    }
}

/// <summary>
/// Локальний обробник permission-based авторизації:
/// 1) Для Keycloak JWT — читає claim "authorization" -> permissions[].scopes[] та перевіряє точний збіг permission;
/// 2) Для Duende IdentityServer OAuth2 JWT — звіряє claim "scope";
/// 3) Для ASP.NET Identity JWT — застосовує матрицю ролей (Admin / User).
/// Валідація виконується повністю локально без звернень до Keycloak/Auth на кожен запит.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // 1. Keycloak Fine-Grained Authorization claim: authorization.permissions[].scopes[]
        var authClaim = context.User.FindFirst("authorization")?.Value;
        if (!string.IsNullOrWhiteSpace(authClaim))
        {
            try
            {
                using var doc = JsonDocument.Parse(authClaim);
                if (doc.RootElement.TryGetProperty("permissions", out var permissionsElement))
                {
                    var scopes = permissionsElement.EnumerateArray()
                        .SelectMany(p => p.TryGetProperty("scopes", out var s)
                            ? s.EnumerateArray().Select(x => x.GetString())
                            : [])
                        .Where(s => !string.IsNullOrWhiteSpace(s));

                    if (scopes.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
                    {
                        context.Succeed(requirement);
                    }
                }
            }
            catch (JsonException)
            {
                // Некоректний формат claim authorization — відмова (403)
            }

            // Якщо це Keycloak-токен із claim authorization, рішення остаточне
            return Task.CompletedTask;
        }

        // 2. Прямий claim "permission" (якщо передано через custom protocol mapper)
        var directPermissions = context.User.FindAll("permission").Select(c => c.Value);
        if (directPermissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Якщо токен виданий Keycloak (DevRealmDrivers) і не містить потрібного permission — fail-closed (403)
        var issuer = context.User.FindFirst("iss")?.Value;
        if (!string.IsNullOrWhiteSpace(issuer) &&
            issuer.Contains("realms/DevRealmDrivers", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        // 3. Duende IdentityServer OAuth2 scopes (orders.read, orders.write, orders_api тощо)
        var oauthScopes = context.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (oauthScopes.Count > 0 && MatchesOAuthScope(oauthScopes, requirement.Permission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 4. ASP.NET Identity ролі (Admin має повний доступ; User — читання каталогу/замовлень і створення замовлень)
        if (context.User.IsInRole("Admin") || context.User.IsInRole("admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if ((context.User.IsInRole("User") || context.User.IsInRole("user")) &&
            requirement.Permission is "orders:read" or "orders:create" or "products:read")
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static bool MatchesOAuthScope(HashSet<string> scopes, string permission)
    {
        var parts = permission.Split(':', 2);
        if (parts.Length != 2)
        {
            return scopes.Contains(permission);
        }

        var resource = parts[0]; // orders / products / payments
        var action = parts[1];   // read / create / update / delete / approve / publish / process / refund

        if (scopes.Contains($"{resource}_api") || scopes.Contains(permission))
        {
            return true;
        }

        return action == "read"
            ? scopes.Contains($"{resource}.read")
            : scopes.Contains($"{resource}.write");
    }
}

/// <summary>
/// Уніфіковане підключення трьох схем JWT-автентифікації (ASP.NET Identity JWT, Duende IdentityServer, Keycloak)
/// через PolicyScheme з автоматичним вибором схеми за issuer (iss) у токені.
/// </summary>
public static class PlatformAuthenticationExtensions
{
    public const string MultiScheme = "PlatformMultiAuth";
    public const string IdentityJwtScheme = "IdentityJwt";
    public const string IdentityServerScheme = "IdentityServer";
    public const string KeycloakScheme = "Keycloak";

    /// <summary>
    /// Автоматичний вибір схеми JWT-автентифікації за issuer (iss) у Bearer-токені.
    /// </summary>
    public static string SelectJwtScheme(Microsoft.AspNetCore.Http.HttpContext context, string identityIssuer)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var rawToken = authHeader["Bearer ".Length..].Trim();
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(rawToken))
            {
                var jwt = handler.ReadJwtToken(rawToken);
                if (string.Equals(jwt.Issuer, identityIssuer, StringComparison.OrdinalIgnoreCase))
                {
                    return IdentityJwtScheme;
                }

                if (jwt.Issuer.Contains("realms/DevRealmDrivers", StringComparison.OrdinalIgnoreCase))
                {
                    return KeycloakScheme;
                }

                return IdentityServerScheme;
            }
        }

        return KeycloakScheme;
    }

    public static IServiceCollection AddPlatformMultiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string apiAudience,
        string keycloakAudience)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var identityIssuer = jwtSection["Issuer"] ?? "Platform.Auth.Api";
        var identityAudience = jwtSection["Audience"] ?? "Platform.Microservices";
        var secretKey = jwtSection["SecretKey"] ?? "SuperSecretPlatformJwtSigningKey_Lab09_AtLeast32Chars!";

        var identityServerAuthority = configuration["IdentityServer:Authority"]
                                      ?? configuration["services:identityserver:http:0"]
                                      ?? "http://localhost:5005";

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = MultiScheme;
            options.DefaultAuthenticateScheme = MultiScheme;
            options.DefaultChallengeScheme = MultiScheme;
        })
        .AddPolicyScheme(MultiScheme, "Select JWT provider by token issuer", options =>
        {
            options.ForwardDefaultSelector = context => SelectJwtScheme(context, identityIssuer);
        })
        // 1. Частина 1: ASP.NET Identity + симетричний JWT (HS256)
        .AddJwtBearer(IdentityJwtScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = identityIssuer,
                ValidateAudience = true,
                ValidAudience = identityAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        })
        // 2. Частина 2: Duende IdentityServer (OAuth 2.0 / OIDC Authority + Discovery)
        .AddJwtBearer(IdentityServerScheme, options =>
        {
            options.Authority = identityServerAuthority;
            options.Audience = apiAudience;
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudiences = [apiAudience, $"{identityServerAuthority.TrimEnd('/')}/resources"]
            };
        })
        // 3. Частина 3: Keycloak через Aspire.Keycloak.Authentication (Service Discovery -> DevRealmDrivers)
        .AddKeycloakJwtBearer(
            serviceName: "keycloak",
            realm: "DevRealmDrivers",
            authenticationScheme: KeycloakScheme,
            configureOptions: options =>
            {
                options.Audience = keycloakAudience;
                if (environment.IsDevelopment())
                {
                    options.RequireHttpsMetadata = false;
                }
            });

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
