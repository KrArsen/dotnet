using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Sales.Api.Middleware;
using Sales.Bll.Caching;
using Sales.Bll.Mapping;
using Sales.Bll.Services;
using Sales.Bll.Validation;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;
using Sales.Dal.UnitOfWork;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Налаштування Serilog структурованого логування
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

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
        Description = "Лабораторна робота №02: Тришарова вертикаль DAL (ADO.NET + Dapper) -> BLL -> API з Unit of Work, ProblemDetails та Aspire."
    });

    c.AddSecurityDefinition("CorrelationId", new OpenApiSecurityScheme
    {
        Description = "Кореляційний ідентифікатор запиту (X-Correlation-Id)",
        Name = "X-Correlation-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
});

// Отримання рядка підключення до SalesDb з перевіркою існування
var connectionString = builder.Configuration.GetConnectionString("SalesDb")
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

// Пам'ять та кешування ідемпотентності
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>();

// Явна реєстрація валідаторів рівня 2 (Scoped)
builder.Services.AddScoped<IClientValidator, ClientValidator>();
builder.Services.AddScoped<IDealValidator, DealValidator>();

// Явна реєстрація бізнес-сервісів BLL (Scoped)
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IDealService, DealService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ITeamMemberService, TeamMemberService>();

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

// Кореляційний Middleware (X-Correlation-Id) для наскрізного трасування
app.UseMiddleware<CorrelationIdMiddleware>();

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

app.MapControllers();

app.Run();

// Публічний клас для WebApplicationFactory в тестах
public partial class Program { }
