using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sales.Api.HealthChecks;

/// <summary>
/// Налаштування комплексних Health Checks для мікросервісу Sales/Orders:
/// - SQL Server (orders-db): легкий запит SELECT 1, failureStatus = Unhealthy, теги ["database", "sql", "ready"], timeout = 5 с
/// - Redis (redis-cache): PING, failureStatus = Degraded (кеш необов'язковий), теги ["cache", "redis", "ready"], timeout = 3 с
/// - gRPC Health Checking Protocol (AddGrpcHealthChecks) для опитування з Aggregator
/// </summary>
public static class SalesHealthChecksExtensions
{
    public static IServiceCollection AddSalesHealthChecks(
        this IServiceCollection services,
        string sqlConnectionString,
        string redisConnectionString)
    {
        services.AddHealthChecks()
            .AddSqlServer(
                connectionString: sqlConnectionString,
                healthQuery: "SELECT 1;",
                name: "orders-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "sql", "ready"],
                timeout: TimeSpan.FromSeconds(5))
            .AddRedis(
                redisConnectionString: redisConnectionString,
                name: "redis-cache",
                failureStatus: HealthStatus.Degraded, // кеш необов'язковий — збій переводить у Degraded, не в Unhealthy
                tags: ["cache", "redis", "ready"],
                timeout: TimeSpan.FromSeconds(3));

        // Стандартний gRPC Health Checking Protocol (grpc.health.v1.Health)
        services.AddGrpcHealthChecks()
            .AddCheck("orders-grpc", () => HealthCheckResult.Healthy("Orders gRPC service is serving"));

        return services;
    }
}
