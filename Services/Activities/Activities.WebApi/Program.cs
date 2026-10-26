using Activities.Application;
using Activities.Application.Common.Mappings;
using Activities.Infrastructure;
using Activities.WebApi.Middleware;
using Activities.WebApi.Mapping;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Налаштування структурованого логування Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({CorrelationId}) {Message:lj}{NewLine}{Exception}");
});

// 2. Глобальна обробка винятків RFC 7807/9457 ProblemDetails
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 3. Реєстрація шару Application (MediatR, Pipeline Behaviors, Caching, FluentValidation)
builder.Services.AddApplication(builder.Configuration);

// 4. Реєстрація шару Infrastructure (MongoDB Driver, Context, Repositories, Queries, Seeding)
builder.Services.AddInfrastructure(builder.Configuration);

// 5. Реєстрація AutoMapper з WebApi та Application профілями
builder.Services.AddAutoMapper(cfg =>
{
    var licenseKey = builder.Configuration["LuckyPenny:LicenseKey"] ?? builder.Configuration["AutoMapper:LicenseKey"];
    if (!string.IsNullOrWhiteSpace(licenseKey))
    {
        cfg.LicenseKey = licenseKey;
    }
    cfg.AddMaps(typeof(WebMappingProfile).Assembly);
    cfg.AddMaps(typeof(ActivityMappingProfile).Assembly);
});

// 6. Контролери та Swagger/OpenAPI
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Activities & Reviews Web API (MongoDB + Clean Architecture + CQRS)",
        Version = "v1",
        Description = "Мікросервіс комунікацій, активностей та відгуків клієнтів (Проєкт №3) предметної області CRM для малого бізнесу"
    });
});

// 7. Health Check для MongoDB
builder.Services.AddHealthChecks()
    .AddMongoDb(
        sp => sp.GetRequiredService<Activities.Infrastructure.Persistence.MongoDbContext>().Database.Client,
        name: "mongodb",
        tags: new[] { "db", "ready" });

var app = builder.Build();

// Побудова конвеєра обробки HTTP-запитів
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Activities & Reviews API v1");
        c.RoutePrefix = "swagger";
    });
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
