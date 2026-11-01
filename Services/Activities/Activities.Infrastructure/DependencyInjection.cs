using Activities.Application.Common.Interfaces;
using Activities.Domain.Interfaces;
using Activities.Infrastructure.Persistence;
using Activities.Infrastructure.Queries;
using Activities.Infrastructure.Repositories;
using Activities.Infrastructure.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Activities.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Реєстрація мапінгів BSON (виконується одноразово при старті)
        MongoMappings.Register();

        // 2. Налаштування параметрів MongoDB (із підтримкою рядків підключення від .NET Aspire)
        services.Configure<MongoDbOptions>(configuration.GetSection(MongoDbOptions.SectionName));
        services.PostConfigure<MongoDbOptions>(options =>
        {
            var aspireConnectionString =
                configuration.GetConnectionString("ActivitiesDb") ??
                configuration.GetConnectionString("ReviewsDb") ??
                configuration.GetConnectionString("mongo");

            if (!string.IsNullOrWhiteSpace(aspireConnectionString))
            {
                options.ConnectionString = aspireConnectionString;
            }
        });

        // 3. Singleton контекст доступу до MongoDB
        services.AddSingleton<MongoDbContext>();

        // 4. Репозиторії (Scoped)
        services.AddScoped<ActivityRepository>();
        services.AddScoped<IActivityRepository>(sp => sp.GetRequiredService<ActivityRepository>());
        services.AddScoped<IReviewRepository>(sp => sp.GetRequiredService<ActivityRepository>());

        // 5. Оптимізовані CQRS Queries (Scoped)
        services.AddScoped<ActivityQueries>();
        services.AddScoped<IActivityQueries>(sp => sp.GetRequiredService<ActivityQueries>());
        services.AddScoped<IReviewQueries>(sp => sp.GetRequiredService<ActivityQueries>());

        // 6. Сідінг та створення індексів
        services.AddScoped<IDataSeeder, DataSeeder>();
        services.AddHostedService<IndexCreationService>();
        services.AddHostedService<SeederHostedService>();

        return services;
    }
}
