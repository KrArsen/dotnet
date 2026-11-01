using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Sales.Bll.Caching;
using Sales.Bll.Mapping;
using Sales.Bll.Services;
using Sales.Bll.Validation;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;
using Sales.Dal.UnitOfWork;
using Sales.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Централізовані налаштування платформи .NET Aspire (Serilog, OpenTelemetry, HealthChecks, ServiceDiscovery)
builder.AddServiceDefaults();

// Додавання стандартної підтримки ProblemDetails та централізованого обробника винятків
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

// Додавання контролерів
builder.Services.AddControllers();

// Реєстрація Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sales Microservice API (CRM Small Business)",
        Version = "v1",
        Description = "Лабораторна робота №02 / №05: Тришарова вертикаль DAL (ADO.NET + Dapper) -> BLL -> API з Unit of Work, ProblemDetails та Aspire."
    });

    c.AddSecurityDefinition("CorrelationId", new OpenApiSecurityScheme
    {
        Description = "Кореляційний ідентифікатор запиту (X-Correlation-Id)",
        Name = "X-Correlation-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
});

// Отримання рядка підключення до SalesDb з конфігурації (передається від Aspire або appsettings.json)
var connectionString = builder.Configuration.GetConnectionString("SalesDb")
    ?? builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'SalesDb' not found in configuration.");

// Налаштування параметрів DAL
builder.Services.Configure<DalOptions>(builder.Configuration.GetSection("DalOptions"));

// Явна реєстрація Unit of Work (Scoped)
builder.Services.AddScoped<IUnitOfWork>(sp =>
{
    var options = sp.GetService<IOptions<DalOptions>>();
    var logger = sp.GetService<ILogger<UnitOfWork>>();
    return new UnitOfWork(connectionString, options, logger);
});

// Репозиторії з поточного Unit of Work для єдиного з'єднання в межах запиту
builder.Services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().Clients);
builder.Services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().Deals);
builder.Services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().Products);
builder.Services.AddScoped(sp => sp.GetRequiredService<IUnitOfWork>().TeamMembers);

// Дворівневе кешування: L1 (IMemoryCache з обмеженням SizeLimit) + L2 (IDistributedCache поверх Redis через Aspire)
builder.AddRedisDistributedCache("redis");
builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);
builder.Services.AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>();
builder.Services.AddSingleton<SalesCacheMetrics>();
builder.Services.AddSingleton<Sales.Api.Metrics.CacheMetrics>();
builder.Services.AddScoped<SalesTwoLevelCacheService>();

// Явна реєстрація валідаторів рівня 2 (Scoped)
builder.Services.AddScoped<IClientValidator, ClientValidator>();
builder.Services.AddScoped<IDealValidator, DealValidator>();

// Явна реєстрація бізнес-сервісів BLL (Scoped) з дворівневим кешем для IDealService
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<DealService>();
builder.Services.AddScoped<IDealService>(sp =>
    new CachedDealService(
        sp.GetRequiredService<DealService>(),
        sp.GetRequiredService<SalesTwoLevelCacheService>(),
        sp.GetRequiredService<ILogger<CachedDealService>>()));
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ITeamMemberService, TeamMemberService>();

// gRPC-сервіс із серверним interceptor-ом логування
builder.Services.AddScoped<Sales.Api.Grpc.OrdersLoggingInterceptor>();
builder.Services.AddGrpc(o =>
{
    o.Interceptors.Add<Microsoft.Extensions.Hosting.LoggingInterceptor>();
});

// Реєстрація AutoMapper з ліцензійним ключем з конфігурації
builder.Services.AddAutoMapper(cfg =>
{
    var licenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    if (!string.IsNullOrWhiteSpace(licenseKey))
    {
        cfg.LicenseKey = licenseKey;
    }

    cfg.AddProfile<ClientProfile>();
    cfg.AddProfile<DealProfile>();
    cfg.AddProfile<ProductProfile>();
    cfg.AddProfile<TeamMemberProfile>();
});

var app = builder.Build();

// Кореляційний Middleware (X-Correlation-Id) для наскрізного трасування з ServiceDefaults
app.UseMiddleware<Microsoft.Extensions.Hosting.CorrelationIdMiddleware>();

// Обробка доменних та неперехоплених помилок через ProblemDetails (400, 404, 409, 500)
app.UseExceptionHandler();

// Swagger документація
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sales API v1");
    c.RoutePrefix = "swagger";
});

// Перенаправлення з кореня на Swagger UI для зручності тестування
app.MapGet("/", () => Results.Redirect("/swagger"));

// Спільні ендпоінти платформи (/health та /alive)
app.MapDefaultEndpoints();

app.MapControllers();
app.MapGrpcService<Sales.Api.Grpc.OrdersGrpcService>();

app.Run();

// Публічний клас для WebApplicationFactory в тестах
public partial class Program { }
