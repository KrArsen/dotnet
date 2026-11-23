using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Спільні централізовані налаштування .NET Aspire платформи (ServiceDefaults):
/// Serilog зі збагаченням та передачею в OpenTelemetry (writeToProviders: true),
/// розподілене трасування (HTTP, gRPC, SqlClient, MongoDB, власні ActivitySource),
/// бізнес-метрики, метрики кешування та Polly (OpenTelemetry Meter API),
/// наскрізний CorrelationId, Service Discovery, Standard Resilience Handler та розділені Health Checks
/// (/health/live, /health/ready, /health/startup, /health, /alive).
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string LivenessEndpointPath = "/health/live";
    private const string ReadinessEndpointPath = "/health/ready";
    private const string StartupEndpointPath = "/health/startup";
    private const string AlivenessEndpointPath = "/alive";

    private static readonly JsonSerializerOptions HealthJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Дозвіл HTTP/2 без TLS (h2c) для gRPC між внутрішніми сервісами у режимі розробки
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        // 1. Serilog — структуроване логування зі збагаченням і передачею в OpenTelemetry
        builder.Services.AddSerilog((services, cfg) => cfg
            .MinimumLevel.Is(builder.Environment.IsDevelopment()
                ? LogEventLevel.Debug
                : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Polly", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.Extensions.Http.Resilience", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", builder.Environment.ApplicationName)
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(new CompactJsonFormatter()),
            writeToProviders: true); // -> OpenTelemetry -> Structured logs в Aspire Dashboard

        // 2. Метрики ефективності кешування (cache.hits / cache.misses) та серверний gRPC LoggingInterceptor
        builder.Services.AddSingleton<CacheMetrics>();
        builder.Services.AddSingleton<LoggingInterceptor>();

        // 3. OpenTelemetry — розподілене трасування та метрики (включно з Polly і бізнес-метрами)
        builder.ConfigureOpenTelemetry();

        // 4. Health Checks (базова liveness перевірка "self" з тегом "live")
        builder.AddDefaultHealthChecks();

        // 5. Service Discovery, Standard Resilience Handler та наскрізний CorrelationId у всіх HttpClient і gRPC-клієнтах
        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Стандартна політика стійкості (RateLimiter -> TotalTimeout -> Retry -> CircuitBreaker -> AttemptTimeout)
            // Для клієнтів з кастомною політикою в Aggregator спершу викликається RemoveAllResilienceHandlers()
            http.AddStandardResilienceHandler();

            // Вирішення логічних імен сервісів (https+http://catalog-api тощо)
            http.AddServiceDiscovery();

            // Автоматичне прокидання X-Correlation-Id у вихідні HTTP- та gRPC-виклики
            http.AddHttpMessageHandler<CorrelationIdHandler>();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: builder.Environment.ApplicationName,
                    serviceVersion: typeof(Extensions).Assembly.GetName().Version?.ToString() ?? "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["service.environment"] = builder.Environment.EnvironmentName,
                    ["host.name"] = Environment.MachineName
                }))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // Вбудована телеметрія Polly (resilience.polly.strategy.events, pipeline/attempt duration)
                    .AddMeter("Polly")
                    // Бізнес-метрики мікросервісів (Counter, Histogram, ObservableGauge)
                    .AddMeter("Orders.Api")
                    .AddMeter("Sales.Api")
                    .AddMeter("Catalog.Api")
                    .AddMeter("Reviews.Api")
                    .AddMeter("Activities.WebApi")
                    .AddMeter("Platform.Aggregator")
                    // Метрики подієвої шини RabbitMQ (публікація, споживання, повтори, DLQ)
                    .AddMeter("Platform.Messaging")
                    // Метрики дворівневого кешування (L1/L2)
                    .AddMeter(CacheMetrics.MeterName)
                    .AddMeter("Sales.Cache")
                    .AddMeter("Activities.Cache")
                    .AddMeter("Platform.Cache")
                    // Налаштування меж кошиків гістограм тривалості операцій (мс)
                    .AddView(instrument =>
                        instrument.Name.EndsWith(".operation.duration", StringComparison.Ordinal)
                            ? new ExplicitBucketHistogramConfiguration
                            {
                                Boundaries = [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000]
                            }
                            : null);
            })
            .WithTracing(tracing =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    tracing.SetSampler(new AlwaysOnSampler());
                }

                tracing
                    .AddSource(builder.Environment.ApplicationName)
                    .AddSource(
                        "Sales.Api",
                        "Sales.Bll",
                        "Orders.Api",
                        "Catalog.Api",
                        "Catalog.Bll",
                        "Activities.WebApi",
                        "Activities.Application",
                        "Reviews.WebApi",
                        "Platform.Aggregator",
                        "Platform.ApiGateway",
                        "Platform.Messaging",
                        "Grpc.Net.Client")
                    .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath) &&
                            !context.Request.Path.StartsWithSegments(AlivenessEndpointPath);
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                        options.RecordException = true;
                    });
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Process is alive"), ["live"]);

        return builder;
    }

    /// <summary>
    /// Публікує розділені endpoints health checks у всіх середовищах (не лише в Development):
    /// - /health/live та /alive: лише liveness-перевірки (тег "live", без зовнішніх залежностей, < 100 мс)
    /// - /health/ready: readiness-перевірки (тег "ready": БД, кеш Redis, downstream gRPC) із деталізацією у JSON
    /// - /health/startup: startup-перевірка готовності сервісу
    /// - /health: повний звіт стану всіх перевірок у JSON (для Aspire Dashboard WithHttpHealthCheck)
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions
        {
            ResponseWriter = WriteHealthJsonResponse
        });

        app.MapHealthChecks(LivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
            ResponseWriter = WriteHealthJsonResponse
        });

        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
            ResponseWriter = WriteHealthJsonResponse
        });

        app.MapHealthChecks(ReadinessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready"),
            ResponseWriter = WriteHealthJsonResponse
        });

        app.MapHealthChecks(StartupEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live") || r.Tags.Contains("ready"),
            ResponseWriter = WriteHealthJsonResponse
        });

        return app;
    }

    /// <summary>
    /// Формує структуровану JSON-відповідь health check із деталями кожної залежності.
    /// </summary>
    public static Task WriteHealthJsonResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            timestampUtc = DateTime.UtcNow,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                tags = entry.Value.Tags,
                error = entry.Value.Exception?.Message,
                data = entry.Value.Data.Count > 0 ? entry.Value.Data : null
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, HealthJsonOptions));
    }
}
