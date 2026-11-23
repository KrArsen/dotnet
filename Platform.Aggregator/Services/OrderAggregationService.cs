using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Catalog.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Orders.Grpc;
using Platform.Aggregator.Metrics;
using Platform.Aggregator.Models;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Reviews.Grpc;
using StackExchange.Redis;

namespace Platform.Aggregator.Services;

public interface IOrderAggregationService
{
    Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct = default);
}

/// <summary>
/// Сервіс агрегації даних (Aggregator Pattern на gRPC + Resilience + Redis Graceful Degradation):
/// 1) Перевіряє кеш Redis (aggregator:order:{orderId}:full) ДО виконання gRPC-викликів (з перехопленням RedisConnectionException/RedisTimeoutException);
/// 2) Виконує паралельні gRPC-виклики (Task.WhenAll) до мікросервісів Orders, Catalog та Reviews;
/// 3) Обробляє часткові відмови (RpcException / BrokenCircuitException / TimeoutRejectedException -> Warnings замість 500);
/// 4) Кешує лише повну агреговану відповідь (Warnings.Count == 0) у Redis з коротким TTL (30 с);
/// 5) Записує бізнес-метрики тривалості та лічильники для успішних і невдалих операцій.
/// </summary>
public class OrderAggregationService : IOrderAggregationService
{
    private static readonly ActivitySource ActivitySource = new("Platform.Aggregator");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan AggregatedCacheTtl = TimeSpan.FromSeconds(30);

    private readonly OrdersService.OrdersServiceClient _ordersGrpc;
    private readonly CatalogService.CatalogServiceClient _catalogGrpc;
    private readonly ReviewsService.ReviewsServiceClient _reviewsGrpc;
    private readonly IDistributedCache _cache;
    private readonly CacheMetrics _cacheMetrics;
    private readonly AggregatorMetrics _aggregatorMetrics;
    private readonly ILogger<OrderAggregationService> _logger;

    public OrderAggregationService(
        OrdersService.OrdersServiceClient ordersGrpc,
        CatalogService.CatalogServiceClient catalogGrpc,
        ReviewsService.ReviewsServiceClient reviewsGrpc,
        IDistributedCache cache,
        CacheMetrics cacheMetrics,
        AggregatorMetrics aggregatorMetrics,
        ILogger<OrderAggregationService> logger)
    {
        _ordersGrpc = ordersGrpc ?? throw new ArgumentNullException(nameof(ordersGrpc));
        _catalogGrpc = catalogGrpc ?? throw new ArgumentNullException(nameof(catalogGrpc));
        _reviewsGrpc = reviewsGrpc ?? throw new ArgumentNullException(nameof(reviewsGrpc));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheMetrics = cacheMetrics ?? throw new ArgumentNullException(nameof(cacheMetrics));
        _aggregatorMetrics = aggregatorMetrics ?? throw new ArgumentNullException(nameof(aggregatorMetrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static string FullOrderCacheKey(long orderId) => $"aggregator:order:{orderId}:full";

    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("AggregateFullOrder");
        activity?.SetTag("order.id", orderId);

        _aggregatorMetrics.RequestStarted();
        var totalSw = Stopwatch.StartNew();
        var cacheKey = FullOrderCacheKey(orderId);

        try
        {
            // 1. Перевірка кешу Redis ДО виконання gRPC-викликів із graceful degradation при недоступності Redis
            byte[]? cachedBytes = null;
            try
            {
                cachedBytes = await _cache.GetAsync(cacheKey, ct);
            }
            catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
            {
                _logger.LogWarning(ex, "Redis unavailable, falling back to downstream gRPC services for {Key}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache read failed for {Key}; falling back to gRPC aggregation", cacheKey);
            }

            if (cachedBytes is { Length: > 0 })
            {
                var cachedResult = JsonSerializer.Deserialize<FullOrderDto>(cachedBytes, JsonOptions);
                if (cachedResult is not null)
                {
                    _cacheMetrics.Hit("L2");
                    activity?.SetTag("cache.hit", true);
                    activity?.SetTag("cache.level", "L2");
                    _logger.LogInformation("Cache {Outcome} ({Level}) for {Key}", "HIT", "L2", cacheKey);

                    totalSw.Stop();
                    _aggregatorMetrics.RecordAggregation("success", "redis", cachedResult.Warnings.Count > 0);
                    _aggregatorMetrics.RecordDuration("aggregate_full_order", success: true, totalSw.Elapsed.TotalMilliseconds);
                    return cachedResult;
                }
            }

            _cacheMetrics.Miss("L2");
            activity?.SetTag("cache.hit", false);
            _logger.LogInformation("Cache {Outcome} ({Level}) for {Key} — invoking gRPC services", "MISS", "L2", cacheKey);

            var warnings = new List<string>();

            // 2. Отримуємо замовлення/угоду через gRPC (без базової сутності агрегувати нічого)
            var orderSw = Stopwatch.StartNew();
            OrderReply orderReply;
            try
            {
                orderReply = await _ordersGrpc.GetOrderAsync(
                    new GetOrderRequest { Id = orderId },
                    cancellationToken: ct);
                orderSw.Stop();
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                orderSw.Stop();
                totalSw.Stop();
                _logger.LogWarning(
                    "Order/Deal {OrderId} was not found in orders-api via gRPC ({Elapsed} ms)",
                    orderId,
                    orderSw.ElapsedMilliseconds);
                _aggregatorMetrics.RecordAggregation("not_found", "grpc", isPartial: false);
                _aggregatorMetrics.RecordDuration("aggregate_full_order", success: true, totalSw.Elapsed.TotalMilliseconds);
                return null;
            }

            var order = MapOrderReply(orderReply);

            _logger.LogInformation(
                "orders-api gRPC responded in {Elapsed} ms for Order {OrderId} (ClientId: {ClientId}, ItemsCount: {ItemsCount}, Stage: {Status})",
                orderSw.ElapsedMilliseconds,
                order.Id,
                order.ClientId,
                order.Items.Count,
                order.Stage);

            var productIds = order.Items
                .Select(i => i.ProductId)
                .Distinct()
                .ToList();

            activity?.SetTag("order.clientId", order.ClientId);
            activity?.SetTag("order.productsCount", productIds.Count);

            // 3. Паралельні gRPC-виклики через Task.WhenAll з передачею CancellationToken у кожному виклику
            var catalogRequest = new GetProductsByIdsRequest { Ids = { productIds } };
            var reviewsRequest = new GetForProductsRequest { ProductIds = { productIds } };
            var activitiesRequest = new GetActivitiesByClientRequest { ClientId = order.ClientId, PageSize = 20 };

            var productsTask = SafeGrpcAsync(
                async () =>
                {
                    if (productIds.Count == 0)
                    {
                        return new ProductsReply();
                    }

                    return await _catalogGrpc.GetProductsByIdsAsync(catalogRequest, cancellationToken: ct);
                },
                "catalog-api",
                warnings,
                ct);

            var reviewsTask = SafeGrpcAsync(
                async () =>
                {
                    if (productIds.Count == 0)
                    {
                        return new ReviewsReply();
                    }

                    return await _reviewsGrpc.GetForProductsAsync(reviewsRequest, cancellationToken: ct);
                },
                "reviews-api",
                warnings,
                ct);

            var activitiesTask = SafeGrpcAsync(
                async () => await _reviewsGrpc.GetActivitiesByClientAsync(activitiesRequest, cancellationToken: ct),
                "activities-api",
                warnings,
                ct);

            await Task.WhenAll(productsTask, reviewsTask, activitiesTask);

            var productsReply = await productsTask;
            var reviewsReply = await reviewsTask;
            var activitiesReply = await activitiesTask;

            var rawProducts = productsReply?.Items.Select(MapProductReply).ToList() ?? [];
            var rawReviews = reviewsReply?.Items.Select(MapReviewReply).ToList() ?? [];
            var rawActivities = activitiesReply?.Items
                .Select(a => MapActivityReply(a, order.ClientId, order.Client?.CompanyName ?? string.Empty))
                .ToList() ?? [];

            // 4. Перевірка узгодженості агрегованих даних: залишаємо лише товари, що належать цьому замовленню
            var productSet = productIds.ToHashSet();
            var matchedProducts = rawProducts
                .Where(p => productSet.Contains(p.Id))
                .ToList();

            var productById = matchedProducts.ToDictionary(p => p.Id);
            var reviewsByProduct = rawReviews
                .GroupBy(r => r.ProductId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var enrichedItems = order.Items.Select(item =>
            {
                productById.TryGetValue(item.ProductId, out var catalogProduct);
                reviewsByProduct.TryGetValue(item.ProductId, out var prodReviews);

                double? avgRating = prodReviews is { Count: > 0 }
                    ? Math.Round(prodReviews.Average(r => r.Rating), 2)
                    : null;

                return new EnrichedOrderItemDto(
                    ProductId: item.ProductId,
                    ProductName: catalogProduct?.Name ?? item.ProductName,
                    Sku: catalogProduct?.Sku,
                    ItemType: catalogProduct?.ItemType,
                    Quantity: item.Quantity,
                    DealUnitPrice: item.UnitPrice,
                    CurrentCatalogPrice: catalogProduct?.Price,
                    Discount: item.Discount,
                    LineTotal: item.TotalPrice,
                    AvailableStock: catalogProduct?.StockQuantity,
                    IsActiveInCatalog: catalogProduct?.IsActive,
                    AverageProductRating: avgRating,
                    ReviewsCount: prodReviews?.Count ?? 0);
            }).ToList();

            double? overallAvgRating = rawReviews.Count > 0
                ? Math.Round(rawReviews.Average(r => r.Rating), 2)
                : null;

            var summary = new AggregationSummaryDto(
                TotalDistinctProducts: productIds.Count,
                CatalogProductsMatched: matchedProducts.Count,
                TotalReviewsCount: rawReviews.Count,
                AverageRating: overallAvgRating,
                TotalClientActivitiesCount: rawActivities.Count,
                IsPartialResponse: warnings.Count > 0);

            activity?.SetTag("aggregation.warningsCount", warnings.Count);

            var result = new FullOrderDto(
                Order: order,
                Products: matchedProducts,
                Reviews: rawReviews,
                Activities: rawActivities.Count > 0 ? rawActivities : null,
                EnrichedItems: enrichedItems,
                Summary: summary,
                Warnings: warnings);

            // 5. Кешуємо в Redis ЛИШЕ повні відповіді (часткові відповіді з Warnings НЕ кешуються)
            if (result.Warnings.Count == 0)
            {
                try
                {
                    var serialized = JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions);
                    await _cache.SetAsync(
                        cacheKey,
                        serialized,
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = AggregatedCacheTtl
                        },
                        ct);
                    _logger.LogInformation("Cached full aggregated response {Key} in Redis (TTL: {Ttl}s)", cacheKey, AggregatedCacheTtl.TotalSeconds);
                }
                catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
                {
                    _logger.LogWarning(ex, "Redis unavailable, aggregated response for {Key} was not cached", cacheKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write aggregated response {Key} to Redis", cacheKey);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Skipping Redis cache for partial aggregated response {Key} due to {WarningsCount} warnings",
                    cacheKey,
                    result.Warnings.Count);
            }

            totalSw.Stop();
            _aggregatorMetrics.RecordAggregation(
                result.Warnings.Count > 0 ? "partial" : "success",
                "grpc",
                result.Warnings.Count > 0);
            _aggregatorMetrics.RecordDuration("aggregate_full_order", success: true, totalSw.Elapsed.TotalMilliseconds);

            return result;
        }
        catch (Exception)
        {
            totalSw.Stop();
            _aggregatorMetrics.RecordAggregation("failure", "grpc", isPartial: false);
            _aggregatorMetrics.RecordDuration("aggregate_full_order", success: false, totalSw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _aggregatorMetrics.RequestCompleted();
        }
    }

    private async Task<T?> SafeGrpcAsync<T>(
        Func<Task<T>> call,
        string service,
        List<string> warnings,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            sw.Stop();
            _logger.LogInformation("{Service} gRPC responded in {Elapsed} ms", service, sw.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (BrokenCircuitException ex)
        {
            sw.Stop();
            _logger.LogWarning(
                ex,
                "{Service} circuit breaker is open (fail-fast after {Elapsed} ms)",
                service,
                sw.ElapsedMilliseconds);

            AddWarning(warnings, $"{service} is unavailable (circuit open)");
            return default;
        }
        catch (TimeoutRejectedException ex)
        {
            sw.Stop();
            _logger.LogWarning(
                ex,
                "{Service} timed out after {Elapsed} ms",
                service,
                sw.ElapsedMilliseconds);

            AddWarning(warnings, $"{service} timed out");
            return default;
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.Internal or StatusCode.Cancelled or StatusCode.ResourceExhausted)
        {
            sw.Stop();
            _logger.LogWarning(
                ex,
                "{Service} gRPC unavailable ({StatusCode}) after {Elapsed} ms",
                service,
                ex.StatusCode,
                sw.ElapsedMilliseconds);

            AddWarning(warnings, $"{service} is unavailable");
            return default;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(
                ex,
                "{Service} is unavailable after {Elapsed} ms",
                service,
                sw.ElapsedMilliseconds);

            AddWarning(warnings, $"{service} is unavailable");
            return default;
        }
    }

    private static void AddWarning(List<string> warnings, string message)
    {
        lock (warnings)
        {
            if (!warnings.Contains(message))
            {
                warnings.Add(message);
            }
        }
    }

    private static OrderDto MapOrderReply(OrderReply reply)
    {
        DateTime.TryParse(reply.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt);

        ClientSummaryDto? client = reply.Client is not null
            ? new ClientSummaryDto(
                reply.Client.Id,
                reply.Client.CompanyName,
                reply.Client.ContactPerson,
                reply.Client.Email,
                reply.Client.Phone,
                string.IsNullOrWhiteSpace(reply.Client.Industry) ? null : reply.Client.Industry)
            : null;

        OwnerSummaryDto? owner = !string.IsNullOrWhiteSpace(reply.OwnerName)
            ? new OwnerSummaryDto(0, reply.OwnerName, reply.OwnerRole, string.Empty)
            : null;

        var items = reply.Items.Select(i => new OrderItemDto(
            DealId: reply.Id,
            ProductId: i.ProductId,
            ProductName: i.ProductName,
            UnitPrice: (decimal)i.UnitPrice,
            Discount: (decimal)i.Discount,
            Quantity: i.Quantity,
            TotalPrice: (decimal)i.TotalPrice)).ToList();

        return new OrderDto(
            Id: reply.Id,
            DealNumber: reply.DealNumber,
            Title: reply.Title,
            ClientId: reply.ClientId,
            OwnerId: 0,
            Stage: reply.Status,
            TotalAmount: (decimal)reply.TotalAmount,
            CreatedAt: createdAt == default ? DateTime.UtcNow : createdAt,
            UpdatedAt: null,
            Items: items,
            Client: client,
            Owner: owner);
    }

    private static ProductDto MapProductReply(ProductReply p) =>
        new(
            Id: p.Id,
            Sku: p.Sku,
            Name: p.Name,
            ItemType: p.ItemType,
            Price: (decimal)p.Price,
            StockQuantity: p.StockQuantity,
            IsActive: p.IsActive);

    private static ReviewDto MapReviewReply(ReviewItemReply r)
    {
        DateTime.TryParse(r.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt);
        return new ReviewDto(
            Id: r.Id,
            ProductId: r.ProductId,
            ProductName: r.ProductName,
            AuthorName: r.AuthorName,
            Rating: r.Rating,
            Text: r.Text,
            CommentsCount: 0,
            Version: 1,
            CreatedAt: createdAt == default ? DateTime.UtcNow : createdAt);
    }

    private static ActivityDto MapActivityReply(ActivityItemReply a, long clientId, string clientName)
    {
        DateTime.TryParse(a.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt);
        return new ActivityDto(
            Id: a.Id,
            Type: a.Type,
            ClientId: clientId,
            ClientName: clientName,
            DealId: null,
            DealTitle: null,
            AuthorId: 0,
            AuthorName: a.AuthorName,
            Text: a.Text,
            Rating: a.Rating > 0 ? a.Rating : null,
            CommentsCount: 0,
            CreatedAt: createdAt == default ? DateTime.UtcNow : createdAt);
    }
}
