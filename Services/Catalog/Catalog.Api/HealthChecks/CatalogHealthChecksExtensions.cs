using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.Api.HealthChecks;

/// <summary>
/// Комплексні Health Checks мікросервісу Catalog:
/// - SQL Server (catalog-db): SELECT 1, failureStatus = Unhealthy, теги ["database", "sql", "ready"], timeout = 5 с
/// - Redis (redis-cache): PING, failureStatus = Degraded (кеш необов'язковий), теги ["cache", "redis", "ready"], timeout = 3 с
/// - gRPC Health Checking Protocol (AddGrpcHealthChecks)
/// </summary>
public static class CatalogHealthChecksExtensions
{
    public static IServiceCollection AddCatalogHealthChecks(
        this IServiceCollection services,
        string sqlConnectionString,
        string redisConnectionString)
    {
        services.AddHealthChecks()
            .AddSqlServer(
                connectionString: sqlConnectionString,
                healthQuery: "SELECT 1;",
                name: "catalog-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "sql", "ready"],
                timeout: TimeSpan.FromSeconds(5))
            .AddRedis(
                redisConnectionString: redisConnectionString,
                name: "redis-cache",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "redis", "ready"],
                timeout: TimeSpan.FromSeconds(3));

        services.AddGrpcHealthChecks()
            .AddCheck("catalog-grpc", () => HealthCheckResult.Healthy("Catalog gRPC service is serving"));

        return services;
    }
}
