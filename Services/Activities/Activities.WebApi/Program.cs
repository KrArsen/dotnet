using Activities.Application;
using Activities.Application.Common.Mappings;
using Activities.Infrastructure;
using Activities.WebApi.HealthChecks;
using Activities.WebApi.IntegrationEvents;
using Activities.WebApi.Mapping;
using Activities.WebApi.Metrics;
using Activities.WebApi.Middleware;
using Microsoft.OpenApi.Models;
using Platform.EventBus.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Централізовані налаштування платформи .NET Aspire (Serilog, OpenTelemetry, HealthChecks, ServiceDiscovery)
builder.AddServiceDefaults();

// 2. Глобальна обробка винятків RFC 7807/9457 ProblemDetails
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 3. Розподілений кеш Redis через Aspire + шар Application (MediatR, Pipeline Behaviors, L1+L2 Caching, FluentValidation)
// DisableHealthChecks = true вимикає стандартну Unhealthy-перевірку Aspire, щоб діяла наша Degraded-перевірка redis-cache
builder.AddRedisDistributedCache("redis", configureSettings: settings => settings.DisableHealthChecks = true);
builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);
builder.Services.AddSingleton<Activities.WebApi.Metrics.CacheMetrics>();
builder.Services.AddSingleton<ReviewMetrics>();
builder.Services.AddApplication(builder.Configuration);

// 4. Реєстрація шару Infrastructure (MongoDB Driver, Context, Repositories, Queries, Seeding)
builder.Services.AddInfrastructure(builder.Configuration);

// 4.1. gRPC-сервіс із серверним interceptor-ом логування
builder.Services.AddScoped<Activities.WebApi.Grpc.ReviewsLoggingInterceptor>();
builder.Services.AddGrpc(o =>
{
    o.Interceptors.Add<Microsoft.Extensions.Hosting.LoggingInterceptor>();
});

// 5. Реєстрація AutoMapper з WebApi та Application профілями
builder.Services.AddAutoMapper(cfg =>
{
    var licenseKey = builder.Configuration["LuckyPenny:LicenseKey"] ?? builder.Configuration["AutoMapper:LicenseKey"];
    if (!string.IsNullOrWhiteSpace(licenseKey))
    {
        cfg.LicenseKey = licenseKey;
    }
    cfg.AddMaps(typeof(WebMappingProfile).Assembly);
    cfg.AddMaps(typeof(ActivityMappingProfile).Assembly);
});

// 6. Контролери та Swagger/OpenAPI
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Activities & Reviews Web API (MongoDB + Clean Architecture + CQRS)",
        Version = "v1",
        Description = "Лабораторна робота №04 / №05: Мікросервіс комунікацій, активностей та відгуків клієнтів (Проєкт №3) предметної області CRM для малого бізнесу"
    });
});

// 7. Комплексні Health Checks: MongoDB (reviews-db), Redis (redis-cache: Degraded) та gRPC Health Checking Protocol
var redisConnectionString = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddActivitiesHealthChecks(redisConnectionString);

// 8. Брокер повідомлень RabbitMQ через Aspire.RabbitMQ.Client (IConnection + health check) та фоновий consumer
builder.AddRabbitMQClient("messaging");
builder.Services.AddRabbitMqEventBus();
builder.Services.AddScoped<ProductRenamedHandler>();
builder.Services.AddScoped<ProductPriceChangedHandler>();
builder.Services.AddScoped<ProductLifecycleHandler>();
builder.Services.AddHostedService<ReviewsCatalogEventsConsumer>();

var app = builder.Build();

// Побудова конвеєра обробки HTTP-запитів
app.UseMiddleware<Microsoft.Extensions.Hosting.CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Activities & Reviews API v1");
        c.RoutePrefix = "swagger";
    });
}

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapDefaultEndpoints();
app.MapControllers();
app.MapGrpcService<Activities.WebApi.Grpc.ReviewsGrpcService>();
app.MapGrpcHealthChecksService();

app.Run();
