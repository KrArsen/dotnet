using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.IdentityServer;
using Platform.IdentityServer.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Централізовані налаштування платформи .NET Aspire
builder.AddServiceDefaults();

// 2. Окрема база даних IdentityServerDb для ізоляції безпеки
var connectionString = builder.Configuration.GetConnectionString("IdentityServerDb")
    ?? "Server=127.0.0.1,1433;Database=IdentityServerDb;User Id=sa;Password=Your_Strong_Password_123!;TrustServerCertificate=True";

var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));

// 3. ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 4. Duende IdentityServer з інтеграцією ASP.NET Identity, ConfigurationStore та OperationalStore (EF Core)
builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseInformationEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseSuccessEvents = true;
    options.EmitStaticAudienceClaim = true;
})
.AddAspNetIdentity<ApplicationUser>()
.AddConfigurationStore(options =>
{
    options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
        sql => sql.MigrationsAssembly(migrationsAssembly));
})
.AddOperationalStore(options =>
{
    options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
        sql => sql.MigrationsAssembly(migrationsAssembly));
    options.EnableTokenCleanup = true;
});

// 5. Health Check для IdentityServerDb
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: connectionString,
        healthQuery: "SELECT 1;",
        name: "identityserver-db",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"],
        timeout: TimeSpan.FromSeconds(5));

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseIdentityServer();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "Platform.IdentityServer (Duende OAuth 2.0 / OpenID Connect)",
    discovery = "/.well-known/openid-configuration",
    login = "/Account/Login"
}));
app.MapDefaultEndpoints();
app.MapDefaultControllerRoute();

// 6. Автоматичні міграції трьох контекстів та сідінг clients/scopes/resources/users
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetService<ILogger<Program>>();
    try
    {
        await SeedData.EnsureSeedDataAsync(scope.ServiceProvider, logger);
    }
    catch (Exception ex)
    {
        logger?.LogWarning(ex, "IdentityServerDb migration/seeding skipped at startup (SQL Server may not be reachable yet).");
    }
}

app.Run();
