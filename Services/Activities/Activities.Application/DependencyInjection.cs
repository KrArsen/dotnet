using System.Reflection;
using Activities.Application.Common.Behaviors;
using Activities.Application.Common.Caching;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Activities.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 1. Кешування в оперативній пам'яті та провайдер версії для інвалідації
        services.AddMemoryCache();
        services.AddSingleton<ICacheVersionProvider, CacheVersionProvider>();

        // 2. Валідатори FluentValidation
        services.AddValidatorsFromAssembly(assembly);

        // 3. AutoMapper
        services.AddAutoMapper(cfg =>
        {
            var licenseKey = configuration["LuckyPenny:LicenseKey"] ?? configuration["AutoMapper:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                cfg.LicenseKey = licenseKey;
            }
        }, assembly);

        // 4. MediatR з Pipeline Behaviors у строгому порядку виконання:
        //    (1) LoggingBehavior (зовнішній) ->
        //    (2) ValidationBehavior ->
        //    (3) CachingBehavior ->
        //    (4) PerformanceBehavior (найближчий до обробника)
        services.AddMediatR(cfg =>
        {
            var licenseKey = configuration["LuckyPenny:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                cfg.LicenseKey = licenseKey;
            }

            cfg.RegisterServicesFromAssembly(assembly);

            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        return services;
    }
}
