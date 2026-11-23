using Microsoft.Extensions.DependencyInjection;
using Platform.EventBus.Metrics;
using Platform.EventBus.Publishing;

namespace Platform.EventBus.DependencyInjection;

public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    /// Реєструє спільні компоненти подієвої шини (IEventPublisher на базі RabbitMQ 7.x та MessagingMetrics).
    /// Підключення IConnection реєструється викликом builder.AddRabbitMQClient("messaging") у Program.cs сервісу.
    /// </summary>
    public static IServiceCollection AddRabbitMqEventBus(this IServiceCollection services)
    {
        services.AddSingleton<MessagingMetrics>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        return services;
    }
}
