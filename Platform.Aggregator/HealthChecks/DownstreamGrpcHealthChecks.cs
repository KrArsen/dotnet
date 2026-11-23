using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

/// <summary>
/// Перевірка стану критичного gRPC-сервісу Catalog через стандартний gRPC Health Checking Protocol (grpc.health.v1.Health).
/// Короткий дедлайн (2 с); RpcException перетворюється на Unhealthy та логується.
/// </summary>
public sealed class CatalogHealthCheck(
    Health.HealthClient client,
    ILogger<CatalogHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var reply = await client.CheckAsync(
                new HealthCheckRequest(),
                deadline: DateTime.UtcNow.AddSeconds(2),
                cancellationToken: ct);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy("catalog-api is serving")
                : HealthCheckResult.Unhealthy($"catalog-api status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "catalog-api gRPC health check failed ({StatusCode})", ex.StatusCode);
            return HealthCheckResult.Unhealthy("catalog-api is unavailable", ex);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "catalog-api gRPC health check failed unexpectedly");
            return HealthCheckResult.Unhealthy("catalog-api is unavailable", ex);
        }
    }
}

/// <summary>
/// Перевірка стану критичного gRPC-сервісу Orders/Sales через стандартний gRPC Health Checking Protocol.
/// </summary>
public sealed class OrdersHealthCheck(
    GrpcClientFactory grpcClientFactory,
    ILogger<OrdersHealthCheck> logger) : IHealthCheck
{
    public const string ClientName = "orders-health";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var client = grpcClientFactory.CreateClient<Health.HealthClient>(ClientName);
            var reply = await client.CheckAsync(
                new HealthCheckRequest(),
                deadline: DateTime.UtcNow.AddSeconds(2),
                cancellationToken: ct);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy("orders-api is serving")
                : HealthCheckResult.Unhealthy($"orders-api status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "orders-api gRPC health check failed ({StatusCode})", ex.StatusCode);
            return HealthCheckResult.Unhealthy("orders-api is unavailable", ex);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "orders-api gRPC health check failed unexpectedly");
            return HealthCheckResult.Unhealthy("orders-api is unavailable", ex);
        }
    }
}

/// <summary>
/// Перевірка стану некритичного gRPC-сервісу Reviews/Activities.
/// Оскільки Aggregator підтримує часткову відповідь без відгуків, недоступність сервісу повертає Degraded (або FailureStatus).
/// </summary>
public sealed class ReviewsHealthCheck(
    GrpcClientFactory grpcClientFactory,
    ILogger<ReviewsHealthCheck> logger) : IHealthCheck
{
    public const string ClientName = "reviews-health";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var client = grpcClientFactory.CreateClient<Health.HealthClient>(ClientName);
            var reply = await client.CheckAsync(
                new HealthCheckRequest(),
                deadline: DateTime.UtcNow.AddSeconds(2),
                cancellationToken: ct);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy("reviews-api is serving")
                : new HealthCheckResult(context.Registration.FailureStatus, $"reviews-api status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "reviews-api gRPC health check failed ({StatusCode})", ex.StatusCode);
            return new HealthCheckResult(context.Registration.FailureStatus, "reviews-api is unavailable", ex);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "reviews-api gRPC health check failed unexpectedly");
            return new HealthCheckResult(context.Registration.FailureStatus, "reviews-api is unavailable", ex);
        }
    }
}
