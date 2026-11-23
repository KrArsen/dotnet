var builder = DistributedApplication.CreateBuilder(args);

// ============================================================================
// 1. Контейнери БД (SQL Server, MongoDB), кешу (Redis) та брокера (RabbitMQ) з WithDataVolume
// ============================================================================

// Пароль SQL Server як параметр-секрет AppHost
var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImage("mcr.microsoft.com/mssql/server", "2022-latest")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Зчитування SQL-скриптів лабораторної №01 (db/p1) для ініціалізації SalesDb (включно з ProcessedEvents)
var repoRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, ".."));
var p1Dir = Path.Combine(repoRoot, "db", "p1");

var schemaPath = Path.Combine(p1Dir, "schema.sql");
var proceduresPath = Path.Combine(p1Dir, "procedures.sql");
var seedPath = Path.Combine(p1Dir, "seed.sql");

var scripts = new List<string>();
if (File.Exists(schemaPath))
{
    scripts.Add(File.ReadAllText(schemaPath));
}
if (File.Exists(proceduresPath))
{
    scripts.Add(File.ReadAllText(proceduresPath));
}
if (File.Exists(seedPath))
{
    scripts.Add(File.ReadAllText(seedPath));
}

var fullInitScript = string.Join("\nGO\n", scripts);

// База даних SalesDb (Проєкт №1, Лаб. 02 — ADO.NET + Dapper)
var salesDb = sql.AddDatabase("SalesDb");
if (!string.IsNullOrWhiteSpace(fullInitScript))
{
    salesDb.WithCreationScript(fullInitScript);
}

// База даних CatalogDb (Проєкт №2, Лаб. 03 — EF Core Code-First)
var catalogDb = sql.AddDatabase("CatalogDb");

// Контейнер MongoDB з окремим постійним сховищем (WithDataVolume)
var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// База даних ActivitiesDb (Проєкт №3, Лаб. 04 — Clean Architecture + CQRS + MongoDB)
var activitiesDb = mongo.AddDatabase("ActivitiesDb");

// Контейнер Redis для централізованого розподіленого кешування L2 (Лаб. 06–07)
var redis = builder.AddRedis("redis")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Брокер повідомлень RabbitMQ з постійним сховищем та Management UI (порт 15672, Лаб. 08)
var rabbit = builder.AddRabbitMQ("messaging")
    .WithDataVolume()
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

// ============================================================================
// 2. Реєстрація мікросервісів із БД, Redis, RabbitMQ та HTTP Health Check (/health)
// ============================================================================

var salesApi = builder.AddProject<Projects.Sales_Api>("sales-api")
    .WithReference(salesDb)
    .WaitFor(salesDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(rabbit)
    .WaitFor(rabbit)
    .WithHttpHealthCheck("/health");

var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(rabbit)
    .WaitFor(rabbit)
    .WithHttpHealthCheck("/health");

var activitiesApi = builder.AddProject<Projects.Activities_WebApi>("activities-api")
    .WithReference(activitiesDb)
    .WaitFor(activitiesDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(rabbit)
    .WaitFor(rabbit)
    .WithHttpHealthCheck("/health");

// ============================================================================
// 3. Реєстрація Aggregator (gRPC-клієнт + Resilience + Redis) та ApiGateway (YARP)
//    WaitFor очікує статус Healthy від кожного залежного сервісу
// ============================================================================

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(salesApi)
    .WithReference(catalogApi)
    .WithReference(activitiesApi)
    .WithReference(redis)
    .WithEnvironment("services__orders-api__http__0", salesApi.GetEndpoint("http"))
    .WithEnvironment("services__orders-api__https__0", salesApi.GetEndpoint("https"))
    .WithEnvironment("services__reviews-api__http__0", activitiesApi.GetEndpoint("http"))
    .WithEnvironment("services__reviews-api__https__0", activitiesApi.GetEndpoint("https"))
    .WaitFor(redis)
    .WaitFor(salesApi)
    .WaitFor(catalogApi)
    .WaitFor(activitiesApi)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(salesApi)
    .WithReference(catalogApi)
    .WithReference(activitiesApi)
    .WithReference(aggregator)
    .WithEnvironment("services__orders-api__http__0", salesApi.GetEndpoint("http"))
    .WithEnvironment("services__reviews-api__http__0", activitiesApi.GetEndpoint("http"))
    .WaitFor(salesApi)
    .WaitFor(catalogApi)
    .WaitFor(activitiesApi)
    .WaitFor(aggregator)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
