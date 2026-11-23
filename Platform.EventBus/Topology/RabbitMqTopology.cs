using RabbitMQ.Client;

namespace Platform.EventBus.Topology;

/// <summary>
/// Декларативна топологія RabbitMQ (exchanges, durable queues, topic bindings, Dead Letter Exchanges та DLQ).
/// Повторне оголошення при старті кожного сервісу ідемпотентне й безпечне.
/// </summary>
public static class RabbitMqTopology
{
    public const string CatalogExchange = "catalog-events";
    public const string CatalogDeadLetterExchange = "catalog-events.dlx";

    public const string OrdersExchange = "orders-events";
    public const string OrdersDeadLetterExchange = "orders-events.dlx";

    public const string OrdersCatalogSyncQueue = "orders.catalog-sync";
    public const string OrdersCatalogSyncDlq = "orders.catalog-sync.dlq";

    public const string ReviewsCatalogSyncQueue = "reviews.catalog-sync";
    public const string ReviewsCatalogSyncDlq = "reviews.catalog-sync.dlq";

    public const string ReviewsOrdersSyncQueue = "reviews.orders-sync";
    public const string ReviewsOrdersSyncDlq = "reviews.orders-sync.dlq";

    public const string ProductCreatedRoutingKey = "product.created";
    public const string ProductPriceChangedRoutingKey = "product.price_changed";
    public const string ProductRenamedRoutingKey = "product.renamed";
    public const string ProductDeletedRoutingKey = "product.deleted";
    public const string ProductAllRoutingPattern = "product.*";

    public const string OrderStatusChangedRoutingKey = "order.status_changed";
    public const string OrderAllRoutingPattern = "order.*";

    public static async Task DeclareTopologyAsync(IChannel ch, CancellationToken ct = default)
    {
        // 1. Topic exchange для подій Каталогу та Fanout DLX
        await ch.ExchangeDeclareAsync(
            exchange: CatalogExchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        await ch.ExchangeDeclareAsync(
            exchange: CatalogDeadLetterExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        // 2. Topic exchange для подій Замовлень та Fanout DLX
        await ch.ExchangeDeclareAsync(
            exchange: OrdersExchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        await ch.ExchangeDeclareAsync(
            exchange: OrdersDeadLetterExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        // 3. Черга сервісу Замовлень (Sales) + DLQ для синхронізації довідника Products
        await ch.QueueDeclareAsync(
            queue: OrdersCatalogSyncQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = CatalogDeadLetterExchange
            },
            cancellationToken: ct);

        await ch.QueueBindAsync(
            queue: OrdersCatalogSyncQueue,
            exchange: CatalogExchange,
            routingKey: ProductAllRoutingPattern,
            cancellationToken: ct);

        await ch.QueueDeclareAsync(
            queue: OrdersCatalogSyncDlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await ch.QueueBindAsync(
            queue: OrdersCatalogSyncDlq,
            exchange: CatalogDeadLetterExchange,
            routingKey: string.Empty,
            cancellationToken: ct);

        // 4. Черга сервісу Відгуків (Activities/Reviews) + DLQ для оновлення productName
        await ch.QueueDeclareAsync(
            queue: ReviewsCatalogSyncQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = CatalogDeadLetterExchange
            },
            cancellationToken: ct);

        await ch.QueueBindAsync(
            queue: ReviewsCatalogSyncQueue,
            exchange: CatalogExchange,
            routingKey: ProductAllRoutingPattern,
            cancellationToken: ct);

        await ch.QueueDeclareAsync(
            queue: ReviewsCatalogSyncDlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await ch.QueueBindAsync(
            queue: ReviewsCatalogSyncDlq,
            exchange: CatalogDeadLetterExchange,
            routingKey: string.Empty,
            cancellationToken: ct);

        // 5. Черга сервісу Відгуків для подій замовлень + DLQ
        await ch.QueueDeclareAsync(
            queue: ReviewsOrdersSyncQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = OrdersDeadLetterExchange
            },
            cancellationToken: ct);

        await ch.QueueBindAsync(
            queue: ReviewsOrdersSyncQueue,
            exchange: OrdersExchange,
            routingKey: OrderAllRoutingPattern,
            cancellationToken: ct);

        await ch.QueueDeclareAsync(
            queue: ReviewsOrdersSyncDlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await ch.QueueBindAsync(
            queue: ReviewsOrdersSyncDlq,
            exchange: OrdersDeadLetterExchange,
            routingKey: string.Empty,
            cancellationToken: ct);
    }
}
