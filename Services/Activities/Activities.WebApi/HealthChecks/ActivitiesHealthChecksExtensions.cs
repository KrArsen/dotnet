using Activities.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Activities.WebApi.HealthChecks;

/// <summary>
/// Комплексні Health Checks мікросервісу Activities/Reviews:
/// - MongoDB (reviews-db): ping, failureStatus = Unhealthy, теги ["database", "mongodb", "ready"], timeout = 5 с
/// - Redis (redis-cache): PING, failureStatus = Degraded (кеш необов'язковий), теги ["cache", "redis", "ready"], timeout = 3 с
/// - gRPC Health Checking Protocol (AddGrpcHealthChecks)
/// </summary>
public static class ActivitiesHealthChecksExtensions
{
    public static IServiceCollection AddActivitiesHealthChecks(
        this IServiceCollection services,
        string redisConnectionString)
    {
        services.AddHealthChecks()
            .AddMongoDb(
                sp => sp.GetRequiredService<MongoDbContext>().Database.Client,
                name: "reviews-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "mongodb", "ready"],
                timeout: TimeSpan.FromSeconds(5))
            .AddRedis(
                redisConnectionString: redisConnectionString,
                name: "redis-cache",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "redis", "ready"],
                timeout: TimeSpan.FromSeconds(3));

        services.AddGrpcHealthChecks()
            .AddCheck("reviews-grpc", () => HealthCheckResult.Healthy("Reviews gRPC service is serving"));

        return services;
    }
}
