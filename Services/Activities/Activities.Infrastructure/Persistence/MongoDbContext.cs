using Activities.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

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

        var client = new MongoClient(settings);
        _database = client.GetDatabase(opt.DatabaseName);
    }

    public IMongoDatabase Database => _database;

    public IMongoCollection<Activity> Activities => _database.GetCollection<Activity>("activities");
    public IMongoCollection<Review> Reviews => _database.GetCollection<Review>("activities");
}
