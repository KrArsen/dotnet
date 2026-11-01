using Activities.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace Activities.Infrastructure.Persistence;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var opt = options.Value;
        var settings = MongoClientSettings.FromConnectionString(opt.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(opt.ServerSelectionTimeoutMS);
        settings.MaxConnectionPoolSize = opt.MaxPoolSize;

        // Підписка драйвера MongoDB на розподілене трасування OpenTelemetry (DiagnosticSources)
        settings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber());

        var client = new MongoClient(settings);
        var url = new MongoUrl(opt.ConnectionString);
        var databaseName = !string.IsNullOrWhiteSpace(url.DatabaseName)
            ? url.DatabaseName
            : opt.DatabaseName;

        _database = client.GetDatabase(databaseName);
    }

    public IMongoDatabase Database => _database;

    public IMongoCollection<Activity> Activities => _database.GetCollection<Activity>("activities");
    public IMongoCollection<Review> Reviews => _database.GetCollection<Review>("activities");
}
