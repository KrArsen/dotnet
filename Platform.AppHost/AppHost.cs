var builder = DistributedApplication.CreateBuilder(args);

// ============================================================================
// 1. Інфраструктура: SQL Server, MongoDB, Redis, RabbitMQ, Keycloak (Aspire)
// ============================================================================

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImage("mcr.microsoft.com/mssql/server", "2022-latest")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Зчитування SQL-скриптів Лаб. 01 (db/p1) для ініціалізації SalesDb
var repoRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, ".."));
var p1Dir = Path.Combine(repoRoot, "db", "p1");
var scripts = new[] { "schema.sql", "procedures.sql", "seed.sql" }
    .Select(f => Path.Combine(p1Dir, f))
    .Where(File.Exists)
    .Select(File.ReadAllText)
    .ToList();
var fullInitScript = string.Join("\nGO\n", scripts);

var salesDb = sql.AddDatabase("SalesDb");
if (!string.IsNullOrWhiteSpace(fullInitScript))
{
    salesDb.WithCreationScript(fullInitScript);
}

var catalogDb = sql.AddDatabase("CatalogDb");

// Частина 1 (Лаб. 09): окрема база IdentityDb для ASP.NET Identity Auth Service
var identityDb = sql.AddDatabase("IdentityDb");

// Частина 2 (Лаб. 09): ізольований SQL Server та база IdentityServerDb для Duende IdentityServer
var identityServerSql = builder.AddSqlServer("identityserver-sql", sqlPassword)
    .WithImage("mcr.microsoft.com/mssql/server", "2022-latest")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var identityServerDb = identityServerSql.AddDatabase("IdentityServerDb");

// MongoDB (ActivitiesDb)
var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var activitiesDb = mongo.AddDatabase("ActivitiesDb");

// Redis (L2 Cache)
var redis = builder.AddRedis("redis")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// RabbitMQ (EventBus + Management UI на 15672)
var rabbit = builder.AddRabbitMQ("messaging")
    .WithDataVolume()
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

// Частина 3 (Лаб. 09, Траєкторія Б): Keycloak через Aspire.Hosting.Keycloak із постійним сховищем
var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// ============================================================================
// 2. Сервіси автентифікації: Auth Service (Identity + JWT) та Duende IdentityServer
// ============================================================================

var authApi = builder.AddProject<Projects.Auth_Api>("auth-api")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithHttpHealthCheck("/health");

var identityServer = builder.AddProject<Projects.Platform_IdentityServer>("identityserver")
    .WithReference(identityServerDb)
    .WaitFor(identityServerDb)
    .WithHttpEndpoint(port: 5005, name: "idp")
    .WithHttpHealthCheck("/health");

// ============================================================================
// 3. Бізнес-мікросервіси з підключенням до БД, Redis, RabbitMQ, IdentityServer і Keycloak
// ============================================================================

var salesApi = builder.AddProject<Projects.Sales_Api>("sales-api")
    .WithReference(salesDb).WaitFor(salesDb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(identityServer)
    .WithHttpHealthCheck("/health");

var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(identityServer)
    .WithHttpHealthCheck("/health");

var activitiesApi = builder.AddProject<Projects.Activities_WebApi>("activities-api")
    .WithReference(activitiesDb).WaitFor(activitiesDb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(identityServer)
    .WithHttpHealthCheck("/health");

var paymentsApi = builder.AddProject<Projects.Payments_Api>("payments-api")
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(identityServer)
    .WithHttpHealthCheck("/health");

// ============================================================================
// 4. Aggregator (Client Credentials Flow + gRPC + Resilience) та ApiGateway (YARP + JWT)
// ============================================================================

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(salesApi)
    .WithReference(catalogApi)
    .WithReference(activitiesApi)
    .WithReference(paymentsApi)
    .WithReference(redis)
    .WithReference(identityServer)
    .WithReference(keycloak)
    .WithEnvironment("services__orders-api__http__0", salesApi.GetEndpoint("http"))
    .WithEnvironment("services__orders-api__https__0", salesApi.GetEndpoint("https"))
    .WithEnvironment("services__reviews-api__http__0", activitiesApi.GetEndpoint("http"))
    .WithEnvironment("services__reviews-api__https__0", activitiesApi.GetEndpoint("https"))
    .WaitFor(redis)
    .WaitFor(identityServer)
    .WaitFor(keycloak)
    .WaitFor(salesApi)
    .WaitFor(catalogApi)
    .WaitFor(activitiesApi)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(authApi)
    .WithReference(identityServer)
    .WithReference(keycloak)
    .WithReference(salesApi)
    .WithReference(catalogApi)
    .WithReference(activitiesApi)
    .WithReference(paymentsApi)
    .WithReference(aggregator)
    .WithEnvironment("services__orders-api__http__0", salesApi.GetEndpoint("http"))
    .WithEnvironment("services__reviews-api__http__0", activitiesApi.GetEndpoint("http"))
    .WaitFor(authApi)
    .WaitFor(salesApi)
    .WaitFor(catalogApi)
    .WaitFor(activitiesApi)
    .WaitFor(paymentsApi)
    .WaitFor(aggregator)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
