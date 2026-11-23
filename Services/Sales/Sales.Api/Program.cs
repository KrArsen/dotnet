using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Platform.EventBus.DependencyInjection;
using Sales.Api.HealthChecks;
using Sales.Api.IntegrationEvents;
using Sales.Api.Metrics;
using Sales.Api.Middleware;
using Sales.Bll.Caching;
using Sales.Bll.Mapping;
using Sales.Bll.Services;
using Sales.Bll.Validation;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;
using Sales.Dal.UnitOfWork;

var builder = WebApplication.CreateBuilder(args);

// 1. Централізовані налаштування платформи .NET Aspire (Serilog, OpenTelemetry, HealthChecks, ServiceDiscovery)
builder.AddServiceDefaults();

// 1.1. Мультипровайдерна JWT-автентифікація: ASP.NET Identity JWT, Duende IdentityServer (Authority/Audience) та Keycloak (DevRealmDrivers)
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
    options.Audience = "orders_api";
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = true,
        ValidAudiences = ["orders_api", $"{identityServerAuthority.TrimEnd('/')}/resources"]
    };
})
.AddKeycloakJwtBearer(
    serviceName: "keycloak",
    realm: "DevRealmDrivers",
    authenticationScheme: PlatformAuthenticationExtensions.KeycloakScheme,
    configureOptions: options =>
    {
        options.Audience = "orders-api";
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
        }
    });

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// 1.2. Політики авторизації: scope-based (IdentityServer) та permission-based [RequirePermission] (Keycloak — Таблиця 8)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("orders.read", p => p.RequireClaim("scope", "orders.read", "orders_api"));
    options.AddPolicy("orders.write", p => p.RequireClaim("scope", "orders.write", "orders_api"));

    options.AddPolicy("orders:read", p => p.Requirements.Add(new PermissionRequirement("orders:read")));
    options.AddPolicy("orders:create", p => p.Requirements.Add(new PermissionRequirement("orders:create")));
    options.AddPolicy("orders:update", p => p.Requirements.Add(new PermissionRequirement("orders:update")));
    options.AddPolicy("orders:delete", p => p.Requirements.Add(new PermissionRequirement("orders:delete")));
    options.AddPolicy("orders:approve", p => p.Requirements.Add(new PermissionRequirement("orders:approve")));
});

// Додавання стандартної підтримки ProblemDetails та централізованого обробника винятків
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

// Додавання контролерів
builder.Services.AddControllers();

// Реєстрація Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sales Microservice API (CRM Small Business)",
        Version = "v1",
        Description = "Лабораторна робота №02 / №05: Тришарова вертикаль DAL (ADO.NET + Dapper) -> BLL -> API з Unit of Work, ProblemDetails та Aspire."
    });

    c.AddSecurityDefinition("CorrelationId", new OpenApiSecurityScheme
    {
        Description = "Кореляційний ідентифікатор запиту (X-Correlation-Id)",
        Name = "X-Correlation-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
});

// Отримання рядка підключення до SalesDb з конфігурації (передається від Aspire або appsettings.json)
var connectionString = builder.Configuration.GetConnectionString("SalesDb")
    ?? builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'SalesDb' not found in configuration.");

// Налаштування параметрів DAL
builder.Services.Configure<DalOptions>(builder.Configuration.GetSection("DalOptions"));

// Явна реєстрація Unit of Work (Scoped)
builder.Services.AddScoped<IUnitOfWork>(sp =>
{
    var options = sp.GetService<IOptions<DalOptions>>();
    var logger = sp.GetService<ILogger<UnitOfWork>>();
    return new UnitOfWork(connectionString, options, logger);
});

// Репозиторії з поточного Unit of Work для єдиного з'єднання в межах запиту
builder.Services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().Clients);
builder.Services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().Deals);
builder.Services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().Products);
builder.Services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().TeamMembers);

// Дворівневе кешування: L1 (IMemoryCache з обмеженням SizeLimit) + L2 (IDistributedCache поверх Redis через Aspire)
// DisableHealthChecks = true вимикає стандартну Unhealthy-перевірку Aspire, щоб діяла наша Degraded-перевірка redis-cache
builder.AddRedisDistributedCache("redis", configureSettings: settings => settings.DisableHealthChecks = true);
builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);
builder.Services.AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>();
builder.Services.AddSingleton<SalesCacheMetrics>();
builder.Services.AddSingleton<Sales.Api.Metrics.CacheMetrics>();
builder.Services.AddSingleton<OrderMetrics>();
builder.Services.AddScoped<SalesTwoLevelCacheService>();

// Health Checks для SQL Server (orders-db), Redis (redis-cache: Degraded) та gRPC Health Checking Protocol
var redisConnectionString = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddSalesHealthChecks(connectionString, redisConnectionString);

// Брокер повідомлень RabbitMQ через Aspire.RabbitMQ.Client (IConnection + health check), publisher та фоновий consumer
builder.AddRabbitMQClient("messaging");
builder.Services.AddRabbitMqEventBus();
builder.Services.AddScoped<ProductPriceChangedHandler>();
builder.Services.AddScoped<ProductRenamedHandler>();
builder.Services.AddScoped<ProductCreatedHandler>();
builder.Services.AddScoped<ProductDeletedHandler>();
builder.Services.AddHostedService<CatalogEventsConsumer>();

// Явна реєстрація валідаторів рівня 2 (Scoped)
builder.Services.AddScoped<IClientValidator, ClientValidator>();
builder.Services.AddScoped<IDealValidator, DealValidator>();

// Явна реєстрація бізнес-сервісів BLL (Scoped) з дворівневим кешем для IDealService
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<DealService>();
builder.Services.AddScoped<IDealService>(sp =>
    new CachedDealService(
        sp.GetRequiredService<DealService>(),
        sp.GetRequiredService<SalesTwoLevelCacheService>(),
        sp.GetRequiredService<ILogger<CachedDealService>>()));
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ITeamMemberService, TeamMemberService>();

// gRPC-сервіс із серверним interceptor-ом логування
builder.Services.AddScoped<Sales.Api.Grpc.OrdersLoggingInterceptor>();
builder.Services.AddGrpc(o =>
{
    o.Interceptors.Add<Microsoft.Extensions.Hosting.LoggingInterceptor>();
});

// Реєстрація AutoMapper з ліцензійним ключем з конфігурації
builder.Services.AddAutoMapper(cfg =>
{
    var licenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    if (!string.IsNullOrWhiteSpace(licenseKey))
    {
        cfg.LicenseKey = licenseKey;
    }

    cfg.AddProfile<ClientProfile>();
    cfg.AddProfile<DealProfile>();
    cfg.AddProfile<ProductProfile>();
    cfg.AddProfile<TeamMemberProfile>();
});

var app = builder.Build();

// Кореляційний Middleware (X-Correlation-Id) для наскрізного трасування з ServiceDefaults
app.UseMiddleware<Microsoft.Extensions.Hosting.CorrelationIdMiddleware>();

// Обробка доменних та неперехоплених помилок через ProblemDetails (400, 404, 409, 500)
app.UseExceptionHandler();

// Swagger документація
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sales API v1");
    c.RoutePrefix = "swagger";
});

// Аутентифікація та авторизація (JWT Identity / IdentityServer / Keycloak + PermissionAuthorizationHandler)
app.UseAuthentication();
app.UseAuthorization();

// Перенаправлення з кореня на Swagger UI для зручності тестування
app.MapGet("/", () => Results.Redirect("/swagger"));

// Спільні ендпоінти платформи (/health, /health/live, /health/ready, /health/startup, /alive)
app.MapDefaultEndpoints();

app.MapControllers();
app.MapGrpcService<Sales.Api.Grpc.OrdersGrpcService>();
app.MapGrpcHealthChecksService();

app.Run();

// Публічний клас для WebApplicationFactory в тестах
public partial class Program { }
