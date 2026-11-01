using System.Text.Json;
using System.Text.Json.Serialization;
using Catalog.Grpc;
using Microsoft.OpenApi.Models;
using Orders.Grpc;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Grpc;
using Platform.Aggregator.Services;
using Reviews.Grpc;

var builder = WebApplication.CreateBuilder(args);

// 1. Спільні налаштування платформи (Serilog, OpenTelemetry, ServiceDiscovery, Resilience, CorrelationIdHandler)
builder.AddServiceDefaults();

// 2. Розподілений кеш Redis для збереження агрегованих відповідей (TTL 30-60 с)
builder.AddRedisDistributedCache("redis");

// 3. Типізовані gRPC-клієнти з логічними іменами сервісів через Aspire Service Discovery
builder.Services.AddTransient<GrpcClientLoggingInterceptor>();

builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(o =>
    o.Address = new Uri("https+http://orders-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>();

builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o =>
    o.Address = new Uri("https+http://catalog-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>();

builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o =>
    o.Address = new Uri("https+http://reviews-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>();

// Typed HttpClients для резервної сумісності з REST-ендпоінтами (Лаб. 05)
builder.Services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"));
builder.Services.AddHttpClient<SalesClient>(c => c.BaseAddress = new("https+http://sales-api"));
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"));
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"));
builder.Services.AddHttpClient<ActivitiesClient>(c => c.BaseAddress = new("https+http://activities-api"));

// 4. Реєстрація сервісу агрегації
builder.Services.AddScoped<IOrderAggregationService, OrderAggregationService>();

// 5. Налаштування JSON серіалізації (camelCase та ігнорування null-значень)
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
        Title = "Platform Aggregator Service API (gRPC + Redis Cache)",
        Version = "v1",
        Description = "Лабораторна робота №05 / №06: Сервіс агрегації даних через паралельні gRPC-виклики (Task.WhenAll), кешування агрегованої відповіді в Redis та обробка часткових відмов."
    });
});

var app = builder.Build();

// 6. Наскрізний CorrelationId Middleware
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
