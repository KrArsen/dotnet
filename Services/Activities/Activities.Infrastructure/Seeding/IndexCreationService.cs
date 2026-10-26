using Activities.Domain.Entities;
using Activities.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Activities.Infrastructure.Seeding;

/// <summary>
/// Фоновий сервіс автоматичного створення індексів MongoDB при запуску застосунку
/// </summary>
public class IndexCreationService : IHostedService
{
    private readonly MongoDbContext _context;
    private readonly ILogger<IndexCreationService> _logger;

    public IndexCreationService(MongoDbContext context, ILogger<IndexCreationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating MongoDB indexes for 'activities' collection...");

            var collection = _context.Activities;

            var indexModels = new List<CreateIndexModel<Activity>>
            {
                // 1. Compound index: { clientId: 1, createdAt: -1 }
                new(
                    Builders<Activity>.IndexKeys.Ascending(x => x.ClientId).Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "idx_activities_client_date" }),

                // 2. Deal index: { dealId: 1 }
                new(
                    Builders<Activity>.IndexKeys.Ascending(x => x.DealId),
                    new CreateIndexOptions { Name = "idx_activities_dealId" }),

                // 3. Author index: { "author.userId": 1 }
                new(
                    Builders<Activity>.IndexKeys.Ascending("author.userId"),
                    new CreateIndexOptions { Name = "idx_activities_author_userId" }),

                // 4. Type index: { type: 1 }
                new(
                    Builders<Activity>.IndexKeys.Ascending(x => x.Type),
                    new CreateIndexOptions { Name = "idx_activities_type" }),

                // 5. Compound unique index: { clientId: 1, "author.userId": 1 }
                new(
                    Builders<Activity>.IndexKeys.Ascending(x => x.ClientId).Ascending("author.userId"),
                    new CreateIndexOptions { Name = "idx_activities_client_author_unique", Unique = true }),

                // 6. Text index on text & clientName
                new(
                    Builders<Activity>.IndexKeys.Text(x => x.Text).Text(x => x.ClientName),
                    new CreateIndexOptions { Name = "idx_activities_text_search" })
            };

            await collection.Indexes.CreateManyAsync(indexModels, cancellationToken);
            _logger.LogInformation("MongoDB indexes for 'activities' successfully created/verified.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create MongoDB indexes. Database might be unavailable during initial startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
