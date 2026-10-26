var builder = DistributedApplication.CreateBuilder(args);

// SQL Server 2022 контейнер (mcr.microsoft.com/mssql/server:2022-latest) з фіксованим розробницьким паролем
var sqlPassword = builder.AddParameter("sql-password", "Your_Strong_Password_123!", secret: true);

var sqlServer = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithImage("mcr.microsoft.com/mssql/server", "2022-latest")
    .WithLifetime(ContainerLifetime.Persistent);

// Зчитування SQL-скриптів Лабораторної №01 (db/p1)
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

// База даних SalesDb із застосуванням повної схеми, процедур та початкових даних Лаб 01
var salesDb = sqlServer.AddDatabase("SalesDb");
if (!string.IsNullOrWhiteSpace(fullInitScript))
{
    salesDb.WithCreationScript(fullInitScript);
}

// Мікросервіс Sales.Api зв'язується з базою SalesDb (автоматичне надання рядка підключення та очікування готовності)
var salesApi = builder.AddProject<Projects.Sales_Api>("sales-api")
    .WithReference(salesDb)
    .WaitFor(salesDb);

// База даних CatalogDb для мікросервісу Catalog (EF Core Code-First)
var catalogDb = sqlServer.AddDatabase("CatalogDb");

// Мікросервіс Catalog.Api зв'язується з базою CatalogDb
var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb);

builder.Build().Run();
