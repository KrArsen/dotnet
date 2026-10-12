using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;
using Sales.Dal.UnitOfWork;

namespace Sales.Dal.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSalesDal(
        this IServiceCollection services,
        string connectionString,
        Action<DalOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddScoped<IUnitOfWork>(sp =>
        {
            var options = sp.GetService<IOptions<DalOptions>>();
            return new UnitOfWork.UnitOfWork(connectionString, options);
        });

        // Репозиторії доступні окремо з поточного UnitOfWork (гарантія єдиного SqlConnection у межах scope)
        services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().Clients);
        services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().Deals);
        services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().Products);
        services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().TeamMembers);

        return services;
    }
}
