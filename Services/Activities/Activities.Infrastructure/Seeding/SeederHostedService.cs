using Activities.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Activities.Infrastructure.Seeding;

/// <summary>
/// Фоновий сервіс запуску сідінгу MongoDB у середовищі розробки
/// </summary>
public class SeederHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<MongoDbOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<SeederHostedService> _logger;

    public SeederHostedService(
        IServiceProvider serviceProvider,
        IOptions<MongoDbOptions> options,
        IHostEnvironment env,
        ILogger<SeederHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_env.IsProduction() || !_options.Value.SeedEnabled)
        {
            _logger.LogInformation("MongoDB data seeding is skipped (Production environment or SeedEnabled=false).");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
