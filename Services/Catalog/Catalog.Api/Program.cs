using Catalog.Api.Middleware;
using Catalog.Api.Validators;
using Catalog.Bll.Caching;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Dal.Context;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Seeding;
using Catalog.Dal.UnitOfWork;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog структуроване логування
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({CorrelationId}) {Message:lj}{NewLine}{Exception}");
});

// 2. Глобальна обробка винятків у форматі ProblemDetails (RFC 7807/9457)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 3. Підключення до бази даних CatalogDb через EF Core SQL Server
var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? "Server=127.0.0.1,1433;Database=CatalogDb;User Id=sa;Password=Your_Strong_Password_123!;TrustServerCertificate=True";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// 4. Реєстрація Unit of Work та репозиторіїв
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// 5. In-Memory кешування
builder.Services.AddMemoryCache();

// 6. Реєстрація AutoMapper з підтримкою конфігурації ліцензійного ключа
builder.Services.AddAutoMapper(cfg =>
{
    var licenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    if (!string.IsNullOrWhiteSpace(licenseKey))
    {
        cfg.LicenseKey = licenseKey;
    }
}, typeof(CatalogMappingProfile).Assembly);

// 7. Реєстрація FluentValidation валідаторів через AddValidatorsFromAssembly
builder.Services.AddValidatorsFromAssembly(typeof(Catalog.Bll.Validation.CreateProductDtoValidator).Assembly);

// 8. Реєстрація BLL-сервісів (з IMemoryCache декоратором для ProductService)
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<IProductService>(sp =>
{
    var concrete = sp.GetRequiredService<ProductService>();
    var cache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<CachedProductService>>();
    return new CachedProductService(concrete, cache, logger);
});
builder.Services.AddScoped<ICategoryService, CategoryService>();

// 9. Контролери та Swagger/OpenAPI
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Catalog Microservice API (CRM Small Business)",
        Version = "v1",
        Description = "Лабораторна робота №03: Мікросервіс Catalog на EF Core Code-First (тришарова архітектура, Unit of Work, DTO + AutoMapper, Specification pattern, Serilog, ProblemDetails)."
    });

    c.AddSecurityDefinition("CorrelationId", new OpenApiSecurityScheme
    {
        Description = "Кореляційний ідентифікатор запиту (X-Correlation-Id)",
        Name = "X-Correlation-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
});

var app = builder.Build();

// 10. HTTP Pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment() || true) // Дозволяємо Swagger для зручності перевірки стенду
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog API v1");
    });
}

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

// 11. Автоматичне застосування міграцій та програмний сідінг у scope
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetService<ILogger<Program>>();
    try
    {
        logger?.LogInformation("Застосування міграцій EF Core до CatalogDb...");
        await db.Database.MigrateAsync();
        logger?.LogInformation("Міграції успішно застосовано.");

        if (!app.Environment.IsProduction())
        {
            await DbInitializer.SeedAsync(db, logger);
        }
    }
    catch (Exception ex)
    {
        logger?.LogWarning(ex, "Попередження: автоматична міграція/сідінг не вдалися під час старту (можливо, локальний SQL Server ще не запущений).");
    }
}

app.Run();
