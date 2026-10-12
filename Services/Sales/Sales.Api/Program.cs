using Microsoft.OpenApi.Models;
using Sales.Api.Middleware;
using Sales.Bll.Extensions;
using Sales.Dal.Extensions;
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

    // Опис заголовків Correlation ID та Idempotency Key
    c.AddSecurityDefinition("CorrelationId", new OpenApiSecurityScheme
    {
        Description = "Кореляційний ідентифікатор запиту (X-Correlation-Id)",
        Name = "X-Correlation-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
});

// Отримання рядка підключення до SalesDb
var connectionString = builder.Configuration.GetConnectionString("SalesDb")
    ?? "Server=localhost,1433;Database=SalesDb;User Id=sa;Password=Your_Strong_Password_123!;TrustServerCertificate=True;Connect Timeout=30;";

// Реєстрація шару даних (Sales.Dal) - РІВНО тут реєструється DAL
builder.Services.AddSalesDal(connectionString);

// Реєстрація бізнес-логіки (Sales.Bll: сервіси, валідатори, кеш ідемпотентності, AutoMapper)
builder.Services.AddSalesBll();

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
