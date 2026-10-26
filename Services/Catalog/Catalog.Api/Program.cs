using Catalog.Dal.Context;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? "Server=127.0.0.1,1433;Database=CatalogDb;User Id=sa;Password=Your_Strong_Password_123!;TrustServerCertificate=True";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();
app.Run();
