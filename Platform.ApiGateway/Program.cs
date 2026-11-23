using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// 1. Централізовані налаштування платформи (Serilog, OpenTelemetry, HealthChecks, ServiceDiscovery)
builder.AddServiceDefaults();
builder.Services.AddHealthChecks()
    .AddCheck("gateway-proxy", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("YARP reverse proxy is ready"), ["ready"]);

// 2. Валідація JWT у Gateway ПЕРЕД проксіюванням (ASP.NET Identity JWT, Duende IdentityServer, Keycloak DevRealmDrivers)
var jwtSection = builder.Configuration.GetSection("Jwt");
var identityIssuer = jwtSection["Issuer"] ?? "Platform.Auth.Api";
var identityAudience = jwtSection["Audience"] ?? "Platform.Microservices";
var secretKey = jwtSection["SecretKey"] ?? "SuperSecretPlatformJwtSigningKey_Lab09_AtLeast32Chars!";
var identityServerAuthority = builder.Configuration["IdentityServer:Authority"]
                              ?? builder.Configuration["services:identityserver:http:0"]
                              ?? "http://localhost:5005";

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = PlatformAuthenticationExtensions.MultiScheme;
    options.DefaultAuthenticateScheme = PlatformAuthenticationExtensions.MultiScheme;
    options.DefaultChallengeScheme = PlatformAuthenticationExtensions.MultiScheme;
})
.AddPolicyScheme(PlatformAuthenticationExtensions.MultiScheme, "Select JWT provider by issuer", options =>
{
    options.ForwardDefaultSelector = ctx => PlatformAuthenticationExtensions.SelectJwtScheme(ctx, identityIssuer);
})
.AddJwtBearer(PlatformAuthenticationExtensions.IdentityJwtScheme, options =>
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
.AddJwtBearer(PlatformAuthenticationExtensions.IdentityServerScheme, options =>
{
    options.Authority = identityServerAuthority;
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = true,
        ValidAudiences = ["orders_api", "products_api", "payments_api", $"{identityServerAuthority.TrimEnd('/')}/resources"]
    };
})
.AddKeycloakJwtBearer(
    serviceName: "keycloak",
    realm: "DevRealmDrivers",
    authenticationScheme: PlatformAuthenticationExtensions.KeycloakScheme,
    configureOptions: options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudiences = ["orders-api", "products-api", "payments-api", "account"]
        };
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
        }
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());
});

// 3. Реєстрація YARP Reverse Proxy з Service Discovery та прокиданням Authorization + X-User-Id
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    .AddTransforms(transformBuilder =>
    {
        transformBuilder.AddRequestTransform(context =>
        {
            // Прокидаємо X-User-Id із валідованого JWT токена до downstream-мікросервісів
            var userId = context.HttpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                         ?? context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? context.HttpContext.User.FindFirstValue("userId");

            if (!string.IsNullOrWhiteSpace(userId))
            {
                context.ProxyRequest.Headers.Remove("X-User-Id");
                context.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);
            }

            // Заголовок Authorization форвардиться автоматично YARP для локальної перевірки прав у сервісах
            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

// 4. Наскрізний CorrelationId Middleware — ДО MapReverseProxy
app.UseMiddleware<CorrelationIdMiddleware>();

// 5. Структуроване логування рішень про маршрутизацію та продуктивності в Gateway
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var correlationId = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();

    try
    {
        await next();
        sw.Stop();

        var proxyFeature = context.GetReverseProxyFeature();
        var route = proxyFeature?.Route.Config.RouteId ?? "unmatched";
        var cluster = proxyFeature?.Route.Config.ClusterId ?? "none";

        if (context.Response.StatusCode >= 500)
        {
            app.Logger.LogWarning(
                "Gateway upstream failure: {Method} {Path} from {ClientIp} -> route {Route} (cluster {Cluster}): {StatusCode} in {Elapsed} ms",
                context.Request.Method, context.Request.Path, clientIp, route, cluster, context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
        else
        {
            app.Logger.LogInformation(
                "{Method} {Path} -> route {Route} (cluster {Cluster}, client {ClientIp}): {StatusCode} in {Elapsed} ms",
                context.Request.Method, context.Request.Path, route, cluster, clientIp, context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
    }
    catch (Exception ex)
    {
        sw.Stop();
        var route = context.GetReverseProxyFeature()?.Route.Config.RouteId ?? "unknown";
        app.Logger.LogError(
            ex,
            "Gateway routing error: {Method} {Path} -> route {Route} failed after {Elapsed} ms [CorrelationId: {CorrelationId}]",
            context.Request.Method, context.Request.Path, route, sw.ElapsedMilliseconds, correlationId);
        throw;
    }
});

// 6. JWT Authentication & Authorization у Gateway перед проксіюванням
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "Platform.ApiGateway (YARP Reverse Proxy + Multi-Provider JWT)",
    status = "Running",
    routes = new[]
    {
        "/auth/{**catch-all} -> auth-api (anonymous)",
        "/users/{**catch-all} -> auth-api (authenticated)",
        "/payments/{**catch-all} -> payments-api (authenticated, admin-only permissions)",
        "/orders/{**catch-all} -> sales-api (authenticated)",
        "/products/{**catch-all} -> catalog-api (authenticated)",
        "/api/aggregator/{**catch-all} -> aggregator (authenticated)",
        "/api/activities/{**catch-all}, /api/reviews/{**catch-all} -> activities-api (authenticated)"
    }
}));

app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();
