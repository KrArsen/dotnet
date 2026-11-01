using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// 1. Централізовані налаштування платформи (Serilog, OpenTelemetry, HealthChecks, ServiceDiscovery)
builder.AddServiceDefaults();

// 2. Реєстрація YARP Reverse Proxy з конфігурації та інтеграція з Aspire Service Discovery
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver(); // http://sales-api, http://catalog-api, http://activities-api -> реальна адреса від Aspire

var app = builder.Build();

// 3. Наскрізний CorrelationId Middleware — реєструється ДО MapReverseProxy
app.UseMiddleware<CorrelationIdMiddleware>();

// 4. Структуроване логування рішень про маршрутизацію та продуктивності в Gateway
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
                context.Request.Method,
                context.Request.Path,
                clientIp,
                route,
                cluster,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
        else
        {
            app.Logger.LogInformation(
                "{Method} {Path} -> route {Route} (cluster {Cluster}, client {ClientIp}): {StatusCode} in {Elapsed} ms",
                context.Request.Method,
                context.Request.Path,
                route,
                cluster,
                clientIp,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
    }
    catch (Exception ex)
    {
        sw.Stop();
        var route = context.GetReverseProxyFeature()?.Route.Config.RouteId ?? "unknown";
        app.Logger.LogError(
            ex,
            "Gateway routing error: {Method} {Path} -> route {Route} failed after {Elapsed} ms [CorrelationId: {CorrelationId}]",
            context.Request.Method,
            context.Request.Path,
            route,
            sw.ElapsedMilliseconds,
            correlationId);
        throw;
    }
});

// Інформаційний ендпоінт кореня шлюзу
app.MapGet("/", () => Results.Ok(new
{
    service = "Platform.ApiGateway (YARP Reverse Proxy)",
    status = "Running",
    routes = new[]
    {
        "/api/aggregator/{**catch-all} -> aggregator",
        "/api/deals/{**catch-all} -> sales-api",
        "/api/clients/{**catch-all} -> sales-api",
        "/api/orders/{**catch-all} -> sales-api (orders-api)",
        "/api/products/{**catch-all} -> catalog-api",
        "/api/categories/{**catch-all} -> catalog-api",
        "/api/activities/{**catch-all} -> activities-api",
        "/api/reviews/{**catch-all} -> activities-api (reviews-api)"
    }
}));

app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();
