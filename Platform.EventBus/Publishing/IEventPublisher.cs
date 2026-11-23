using Platform.EventBus.Events;

namespace Platform.EventBus.Publishing;

/// <summary>
/// Абстракція публікації інтеграційних подій у брокер повідомлень.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(
        T @event,
        string exchange,
        string routingKey,
        CancellationToken ct = default)
        where T : IntegrationEvent;
}

/// <summary>
/// Заглушка IEventPublisher для модульних тестів без підключення до RabbitMQ.
/// </summary>
public sealed class NullEventPublisher : IEventPublisher
{
    public static readonly NullEventPublisher Instance = new();

    public Task PublishAsync<T>(
        T @event,
        string exchange,
        string routingKey,
        CancellationToken ct = default)
        where T : IntegrationEvent =>
        Task.CompletedTask;
}
