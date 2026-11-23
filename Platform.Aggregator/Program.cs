using System.Text.Json;
using System.Text.Json.Serialization;
using Catalog.Grpc;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.OpenApi.Models;
using Orders.Grpc;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Grpc;
using Platform.Aggregator.HealthChecks;
using Platform.Aggregator.Metrics;
using Platform.Aggregator.Services;
using Polly;
using Reviews.Grpc;

var builder = WebApplication.CreateBuilder(args);

// 1. Спільні налаштування платформи (Serilog, OpenTelemetry + Polly Meter, ServiceDiscovery, StandardResilienceHandler, CorrelationId)
builder.AddServiceDefaults();

// 2. Розподілений кеш Redis для збереження агрегованих відповідей (TTL 30 с, graceful degradation при збої Redis)
// DisableHealthChecks = true вимикає стандартну Unhealthy-перевірку Aspire, щоб діяла наша Degraded-перевірка redis-cache
builder.AddRedisDistributedCache("redis", configureSettings: settings => settings.DisableHealthChecks = true);

// 3. Бізнес- та операційні метрики Aggregator (Singleton)
builder.Services.AddSingleton<AggregatorMetrics>();
builder.Services.AddTransient<GrpcClientLoggingInterceptor>();

#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers (експериментальний API Microsoft.Extensions.Http.Resilience)

// ============================================================================
// 4. Типізовані gRPC-клієнти з диференційованими політиками Resilience:
//    - RemoveAllResilienceHandlers() перед кастомним AddStandardResilienceHandler(),
//      щоб не нашаровувати обробники поверх ConfigureHttpClientDefaults
//    - ServiceConfig + RetryPolicy для повторів за gRPC-статусами (HTTP 200 + трейлери)
// ============================================================================

// КРИТИЧНИЙ СЕРВІС: Orders / Sales (без базової угоди агрегація неможлива)
builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(o =>
    o.Address = new Uri("https+http://orders-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>()
    .ConfigureChannel(channel => channel.ServiceConfig = CreateGrpcRetryServiceConfig(
        maxAttempts: 4,
        initialBackoff: TimeSpan.FromMilliseconds(500),
        maxBackoff: TimeSpan.FromSeconds(4),
        StatusCode.Unavailable,
        StatusCode.ResourceExhausted))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler()
    .Configure((o, sp) => ConfigureCriticalOrdersResilience(o, sp, "orders-api-grpc", disableUnsafeHttpMethods: false));

// КРИТИЧНИЙ СЕРВІС: Catalog (без даних товарів не зібрати збагачену картку замовлення)
builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o =>
    o.Address = new Uri("https+http://catalog-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>()
    .ConfigureChannel(channel => channel.ServiceConfig = CreateGrpcRetryServiceConfig(
        maxAttempts: 5,
        initialBackoff: TimeSpan.FromMilliseconds(500),
        maxBackoff: TimeSpan.FromSeconds(4),
        StatusCode.Unavailable,
        StatusCode.ResourceExhausted,
        StatusCode.DeadlineExceeded))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler()
    .Configure((o, sp) => ConfigureCriticalCatalogResilience(o, sp, "catalog-api-grpc", disableUnsafeHttpMethods: false));

// НЕКРИТИЧНИЙ СЕРВІС: Reviews / Activities (швидко здатися та повернути часткову відповідь із Warnings)
builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o =>
    o.Address = new Uri("https+http://reviews-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>()
    .ConfigureChannel(channel => channel.ServiceConfig = CreateGrpcRetryServiceConfig(
        maxAttempts: 2,
        initialBackoff: TimeSpan.FromMilliseconds(300),
        maxBackoff: TimeSpan.FromSeconds(1),
        StatusCode.Unavailable))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler()
    .Configure((o, sp) => ConfigureNonCriticalReviewsResilience(o, sp, "reviews-api-grpc", disableUnsafeHttpMethods: false));

// ============================================================================
// 5. Typed HttpClients (REST-резерв із Лаб. 05) з кастомними політиками та DisableForUnsafeHttpMethods()
// ============================================================================

builder.Services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler()
    .Configure((o, sp) => ConfigureCriticalOrdersResilience(o, sp, "orders-api-http", disableUnsafeHttpMethods: true));

builder.Services.AddHttpClient<SalesClient>(c => c.BaseAddress = new("https+http://sales-api"))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler()
    .Configure((o, sp) => ConfigureCriticalOrdersResilience(o, sp, "sales-api-http", disableUnsafeHttpMethods: true));

builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler()
    .Configure((o, sp) => ConfigureCriticalCatalogResilience(o, sp, "catalog-api-http", disableUnsafeHttpMethods: true));

builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler()
    .Configure((o, sp) => ConfigureNonCriticalReviewsResilience(o, sp, "reviews-api-http", disableUnsafeHttpMethods: true));

builder.Services.AddHttpClient<ActivitiesClient>(c => c.BaseAddress = new("https+http://activities-api"))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler()
    .Configure((o, sp) => ConfigureNonCriticalReviewsResilience(o, sp, "activities-api-http", disableUnsafeHttpMethods: true));

// ============================================================================
// 6. Health Checks для gRPC-залежностей (стандартний протокол grpc.health.v1.Health) та Redis (Degraded)
// ============================================================================

// Клієнти для health check працюють без повторів (fail-fast за коротким дедлайном 2 с)
builder.Services.AddGrpcClient<Health.HealthClient>(o =>
    o.Address = new Uri("https+http://catalog-api"))
    .RemoveAllResilienceHandlers();

builder.Services.AddGrpcClient<Health.HealthClient>(OrdersHealthCheck.ClientName, o =>
    o.Address = new Uri("https+http://orders-api"))
    .RemoveAllResilienceHandlers();

builder.Services.AddGrpcClient<Health.HealthClient>(ReviewsHealthCheck.ClientName, o =>
    o.Address = new Uri("https+http://reviews-api"))
    .RemoveAllResilienceHandlers();

#pragma warning restore EXTEXP0001

var redisConnectionString = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";

builder.Services.AddHealthChecks()
    .AddCheck<CatalogHealthCheck>(
        "catalog-api",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["grpc", "downstream", "ready"],
        timeout: TimeSpan.FromSeconds(3))
    .AddCheck<OrdersHealthCheck>(
        "orders-api",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["grpc", "downstream", "ready"],
        timeout: TimeSpan.FromSeconds(3))
    .AddCheck<ReviewsHealthCheck>(
        "reviews-api",
        failureStatus: HealthStatus.Degraded, // некритична залежність — працює часткова відповідь
        tags: ["grpc", "downstream", "ready"],
        timeout: TimeSpan.FromSeconds(3))
    .AddRedis(
        redisConnectionString: redisConnectionString,
        name: "redis-cache",
        failureStatus: HealthStatus.Degraded, // кеш необов'язковий — недоступність не робить сервіс Unhealthy
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(3));

// 7. Реєстрація сервісу агрегації
builder.Services.AddScoped<IOrderAggregationService, OrderAggregationService>();

// 8. Налаштування JSON серіалізації (camelCase та ігнорування null-значень)
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Platform Aggregator Service API (gRPC + Resilience + Health Checks)",
        Version = "v1",
        Description = "Лабораторна робота №05–07: Агрегація через паралельні gRPC-виклики, диференційовані політики Polly/gRPC resilience, кешування Redis із graceful degradation та перевірка стану залежностей."
    });
});

var app = builder.Build();

// 9. Наскрізний CorrelationId Middleware
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aggregator API v1");
    c.RoutePrefix = "swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapDefaultEndpoints();
app.MapControllers();

app.Run();

// ============================================================================
// Допоміжні фабрики політик Resilience (задокументовано в README.md)
// ============================================================================

static ServiceConfig CreateGrpcRetryServiceConfig(
    int maxAttempts,
    TimeSpan initialBackoff,
    TimeSpan maxBackoff,
    params StatusCode[] retryableStatusCodes)
{
    var policy = new RetryPolicy
    {
        MaxAttempts = maxAttempts,
        InitialBackoff = initialBackoff,
        MaxBackoff = maxBackoff,
        BackoffMultiplier = 2
    };

    foreach (var code in retryableStatusCodes)
    {
        policy.RetryableStatusCodes.Add(code);
    }

    return new ServiceConfig
    {
        MethodConfigs =
        {
            new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = policy
            }
        }
    };
}

// Критичний: Каталог — більше повторів (5), чутливіший Circuit Breaker (FailureRatio = 0.3)
static void ConfigureCriticalCatalogResilience(
    HttpStandardResilienceOptions o,
    IServiceProvider sp,
    string clientName,
    bool disableUnsafeHttpMethods)
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Platform.Aggregator.Resilience");

    o.Retry.MaxRetryAttempts = 5;
    o.Retry.Delay = TimeSpan.FromSeconds(1);
    o.Retry.BackoffType = DelayBackoffType.Exponential;
    o.Retry.UseJitter = true;
    if (disableUnsafeHttpMethods)
    {
        o.Retry.DisableForUnsafeHttpMethods();
    }

    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(35);

    o.CircuitBreaker.FailureRatio = 0.3; // чутливіший для критичного сервісу
    o.CircuitBreaker.MinimumThroughput = 10; // навчальний стенд: менше навантаження
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30); // >= 2 * AttemptTimeout (10 с)
    o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

    AttachResilienceLogging(o, logger, clientName);
}

// Критичний: Замовлення — 3 повтори, збалансований Circuit Breaker (FailureRatio = 0.5)
static void ConfigureCriticalOrdersResilience(
    HttpStandardResilienceOptions o,
    IServiceProvider sp,
    string clientName,
    bool disableUnsafeHttpMethods)
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Platform.Aggregator.Resilience");

    o.Retry.MaxRetryAttempts = 3;
    o.Retry.Delay = TimeSpan.FromSeconds(1);
    o.Retry.BackoffType = DelayBackoffType.Exponential;
    o.Retry.UseJitter = true;
    if (disableUnsafeHttpMethods)
    {
        o.Retry.DisableForUnsafeHttpMethods();
    }

    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(25);

    o.CircuitBreaker.FailureRatio = 0.5;
    o.CircuitBreaker.MinimumThroughput = 10;
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30); // >= 2 * AttemptTimeout (10 с)
    o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

    AttachResilienceLogging(o, logger, clientName);
}

// Некритичний: Відгуки/Активності — 1 повтор, короткий таймаут (2 с / 6 с), висока толерантність (FailureRatio = 0.7)
static void ConfigureNonCriticalReviewsResilience(
    HttpStandardResilienceOptions o,
    IServiceProvider sp,
    string clientName,
    bool disableUnsafeHttpMethods)
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Platform.Aggregator.Resilience");

    o.Retry.MaxRetryAttempts = 1;
    o.Retry.Delay = TimeSpan.FromMilliseconds(500);
    o.Retry.BackoffType = DelayBackoffType.Exponential;
    o.Retry.UseJitter = true;
    if (disableUnsafeHttpMethods)
    {
        o.Retry.DisableForUnsafeHttpMethods();
    }

    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(6);

    o.CircuitBreaker.FailureRatio = 0.7; // менш чутливий — швидко здається і повертає часткову відповідь
    o.CircuitBreaker.MinimumThroughput = 10;
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30); // >= 2 * AttemptTimeout (4 с)
    o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);

    AttachResilienceLogging(o, logger, clientName);
}

static void AttachResilienceLogging(
    HttpStandardResilienceOptions o,
    ILogger logger,
    string clientName)
{
    o.Retry.OnRetry = args =>
    {
        logger.LogWarning(
            args.Outcome.Exception,
            "Resilience retry #{AttemptNumber} for {Client} after {DelayMs} ms (StatusCode: {StatusCode})",
            args.AttemptNumber + 1,
            clientName,
            Math.Round(args.RetryDelay.TotalMilliseconds, 1),
            args.Outcome.Result?.StatusCode);
        return ValueTask.CompletedTask;
    };

    o.CircuitBreaker.OnOpened = args =>
    {
        logger.LogError(
            args.Outcome.Exception,
            "Circuit opened for {Client} for {Duration}",
            clientName,
            args.BreakDuration);
        return ValueTask.CompletedTask;
    };

    o.CircuitBreaker.OnHalfOpened = _ =>
    {
        logger.LogWarning("Circuit half-opened for {Client}: allowing probe request", clientName);
        return ValueTask.CompletedTask;
    };

    o.CircuitBreaker.OnClosed = _ =>
    {
        logger.LogInformation("Circuit closed for {Client}: normal traffic resumed", clientName);
        return ValueTask.CompletedTask;
    };
}
