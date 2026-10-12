using Microsoft.Extensions.DependencyInjection;
using Sales.Bll.Caching;
using Sales.Bll.Mapping;
using Sales.Bll.Services;
using Sales.Bll.Validation;

namespace Sales.Bll.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSalesBll(this IServiceCollection services)
    {
        // Пам'ять, логування та кешування ідемпотентності
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>();

        // AutoMapper конфігурація
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<ClientProfile>();
            cfg.AddProfile<DealProfile>();
            cfg.AddProfile<ProductProfile>();
            cfg.AddProfile<TeamMemberProfile>();
        });

        // Доменна валідація (Рівень 2)
        services.AddScoped<IClientValidator, ClientValidator>();
        services.AddScoped<IDealValidator, DealValidator>();

        // Бізнес-сервіси
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IDealService, DealService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ITeamMemberService, TeamMemberService>();

        return services;
    }
}
