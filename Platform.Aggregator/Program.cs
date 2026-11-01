using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Спільні налаштування платформи (Serilog, OpenTelemetry, ServiceDiscovery, Resilience, CorrelationIdHandler)
builder.AddServiceDefaults();

// 2. Typed HttpClients для кожного мікросервісу через Aspire Service Discovery
builder.Services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"));
builder.Services.AddHttpClient<SalesClient>(c => c.BaseAddress = new("https+http://sales-api"));
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"));
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"));
builder.Services.AddHttpClient<ActivitiesClient>(c => c.BaseAddress = new("https+http://activities-api"));

// 3. Реєстрація сервісу агрегації
builder.Services.AddScoped<IOrderAggregationService, OrderAggregationService>();

// 4. Налаштування JSON серіалізації (camelCase та ігнорування null-значень)
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
        Title = "Platform Aggregator Service API",
        Version = "v1",
        Description = "Лабораторна робота №05: Сервіс агрегації даних (Aggregator Pattern) з паралельними викликами Task.WhenAll та обробкою часткових відмов."
    });
});

var app = builder.Build();

// 5. Наскрізний CorrelationId Middleware
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
