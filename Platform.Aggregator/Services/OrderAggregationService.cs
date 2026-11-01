using System.Diagnostics;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

public interface IOrderAggregationService
{
    Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct = default);
}

/// <summary>
/// Сервіс агрегації даних (Aggregator Pattern):
/// виконує паралельні виклики (Task.WhenAll) до мікросервісів Sales/Orders, Catalog та Activities/Reviews,
/// вимірює тривалість викликів та безпечно обробляє часткові відмови (SafeAsync -> Warnings).
/// </summary>
public class OrderAggregationService : IOrderAggregationService
{
    private static readonly ActivitySource ActivitySource = new("Platform.Aggregator");

    private readonly OrdersClient _orders;
    private readonly CatalogClient _catalog;
    private readonly ReviewsClient _reviews;
    private readonly ActivitiesClient _activities;
    private readonly ILogger<OrderAggregationService> _logger;

    public OrderAggregationService(
        OrdersClient orders,
        CatalogClient catalog,
        ReviewsClient reviews,
        ActivitiesClient activities,
        ILogger<OrderAggregationService> logger)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _reviews = reviews ?? throw new ArgumentNullException(nameof(reviews));
        _activities = activities ?? throw new ArgumentNullException(nameof(activities));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("AggregateFullOrder");
        activity?.SetTag("order.id", orderId);

        var warnings = new List<string>();

        // 1. Отримуємо замовлення/угоду (без базової сутності агрегувати нічого)
        var orderSw = Stopwatch.StartNew();
        var order = await _orders.GetOrderAsync(orderId, ct);
        orderSw.Stop();

        if (order is null)
        {
            _logger.LogWarning("Order/Deal {OrderId} was not found in orders-api ({Elapsed} ms)", orderId, orderSw.ElapsedMilliseconds);
            return null;
        }

        _logger.LogInformation(
            "orders-api responded in {Elapsed} ms for Order {OrderId} (ClientId: {ClientId}, ItemsCount: {ItemsCount}, Stage: {Status})",
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

        // 2. Паралельне опитування залежних мікросервісів через Task.WhenAll (без блокувань .Result чи Task.WaitAll)
        var productsTask = SafeAsync(
            () => _catalog.GetProductsAsync(productIds, ct),
            "catalog-api",
            warnings,
            ct);

        var reviewsTask = SafeAsync(
            () => _reviews.GetForProductsAsync(productIds, ct),
            "reviews-api",
            warnings,
            ct);

        var activitiesTask = SafeAsync(
            () => _activities.GetByClientAsync(order.ClientId, ct),
            "activities-api",
            warnings,
            ct);

        await Task.WhenAll(productsTask, reviewsTask, activitiesTask);

        var rawProducts = await productsTask ?? [];
        var rawReviews = await reviewsTask ?? [];
        var rawActivities = await activitiesTask ?? [];

        // 3. Перевірка узгодженості агрегованих даних: залишаємо лише товари, що належать цьому замовленню
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

        return new FullOrderDto(
            Order: order,
            Products: matchedProducts,
            Reviews: rawReviews,
            Activities: rawActivities.Count > 0 ? rawActivities : null,
            EnrichedItems: enrichedItems,
            Summary: summary,
            Warnings: warnings);
    }

    private async Task<T?> SafeAsync<T>(
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
            _logger.LogInformation("{Service} responded in {Elapsed} ms", service, sw.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Якщо сам клієнт скасував HTTP-запит, зупиняємо виконання
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(
                ex,
                "{Service} is unavailable after {Elapsed} ms",
                service,
                sw.ElapsedMilliseconds);

            lock (warnings)
            {
                if (!warnings.Contains($"{service} is unavailable"))
                {
                    warnings.Add($"{service} is unavailable");
                }
            }

            return default;
        }
    }
}
