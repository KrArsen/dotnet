using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Централізовані налаштування платформи .NET Aspire
builder.AddServiceDefaults();

// 2. Мультипровайдерна JWT-автентифікація: ASP.NET Identity JWT, Duende IdentityServer (Authority/Audience) та Keycloak (DevRealmDrivers)
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
    options.Audience = "payments_api";
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = true,
        ValidAudiences = ["payments_api", $"{identityServerAuthority.TrimEnd('/')}/resources"]
    };
})
.AddKeycloakJwtBearer(
    serviceName: "keycloak",
    realm: "DevRealmDrivers",
    authenticationScheme: PlatformAuthenticationExtensions.KeycloakScheme,
    configureOptions: options =>
    {
        options.Audience = "payments-api";
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
        }
    });

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// 3. Політики авторизації: scope-based (IdentityServer) та permission-based (Keycloak — Таблиця 10)
builder.Services.AddAuthorization(options =>
{
    // Scope-based політики для Duende IdentityServer
    options.AddPolicy("payments.read", p => p.RequireClaim("scope", "payments.read", "payments_api"));
    options.AddPolicy("payments.write", p => p.RequireClaim("scope", "payments.write", "payments_api"));

    // Permission-based політики [RequirePermission] — доступні лише ролі admin
    options.AddPolicy("payments:read", p => p.Requirements.Add(new PermissionRequirement("payments:read")));
    options.AddPolicy("payments:process", p => p.Requirements.Add(new PermissionRequirement("payments:process")));
    options.AddPolicy("payments:refund", p => p.Requirements.Add(new PermissionRequirement("payments:refund")));
});

builder.Services.AddHealthChecks()
    .AddCheck("payments-ready", () => HealthCheckResult.Healthy("Payments API is ready"), ["ready"]);

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payments Microservice API (Permission-Based Demo)",
        Version = "v1",
        Description = "Лабораторна робота №09: Демо-сервіс платежів із перевіркою дозволів payments:read, payments:process, payments:refund (доступно лише admin)."
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payments API v1"));

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
