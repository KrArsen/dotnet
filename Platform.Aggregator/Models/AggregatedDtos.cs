namespace Platform.Aggregator.Models;

/// <summary>
/// Детальна інформація про клієнта з мікросервісу Sales (Orders)
/// </summary>
public record ClientSummaryDto(
    long Id,
    string CompanyName,
    string? ContactPerson,
    string Email,
    string? Phone,
    string? Industry);

/// <summary>
/// Відповідальний менеджер з мікросервісу Sales (Orders)
/// </summary>
public record OwnerSummaryDto(
    long Id,
    string FullName,
    string Email,
    string Role);

/// <summary>
/// Позиція угоди/замовлення з мікросервісу Sales (Orders)
/// </summary>
public record OrderItemDto(
    long DealId,
    long ProductId,
    string ProductName,
    decimal UnitPrice,
    decimal Discount,
    int Quantity,
    decimal TotalPrice);

/// <summary>
/// Модель угоди/замовлення з мікросервісу Sales (Orders)
/// </summary>
public record OrderDto(
    long Id,
    string DealNumber,
    string Title,
    long ClientId,
    long OwnerId,
    string Stage,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<OrderItemDto> Items,
    ClientSummaryDto? Client = null,
    OwnerSummaryDto? Owner = null);

/// <summary>
/// Деталі продукту з мікросервісу Catalog
/// </summary>
public record ProductDetailDto(
    long ProductId,
    string Description,
    string Unit,
    int? DurationMinutes,
    decimal TaxRate);

/// <summary>
/// Категорія з мікросервісу Catalog
/// </summary>
public record CategorySummaryDto(
    long Id,
    string Name,
    string Slug);

/// <summary>
/// Актуальні дані товару/послуги з мікросервісу Catalog
/// </summary>
public record ProductDto(
    long Id,
    string Sku,
    string Name,
    string ItemType,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    ProductDetailDto? Detail = null,
    IReadOnlyList<CategorySummaryDto>? Categories = null);

/// <summary>
/// Відгук на товар/послугу з мікросервісу Activities / Reviews (MongoDB)
/// </summary>
public record ReviewDto(
    string Id,
    long ProductId,
    string ProductName,
    string AuthorName,
    int Rating,
    string Text,
    int CommentsCount,
    int Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null);

/// <summary>
/// Активність/комунікація по клієнту або угоді з мікросервісу Activities (MongoDB)
/// </summary>
public record ActivityDto(
    string Id,
    string Type,
    long ClientId,
    string ClientName,
    long? DealId,
    string? DealTitle,
    long AuthorId,
    string AuthorName,
    string Text,
    int? Rating,
    int CommentsCount,
    DateTime CreatedAt);

/// <summary>
/// Узагальнена обгортка пагінованої відповіді мікросервісів
/// </summary>
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Збагачена позиція замовлення/угоди: поєднує дані угоди (Sales), каталогу (Catalog) та відгуків (Reviews)
/// </summary>
public record EnrichedOrderItemDto(
    long ProductId,
    string ProductName,
    string? Sku,
    string? ItemType,
    int Quantity,
    decimal DealUnitPrice,
    decimal? CurrentCatalogPrice,
    decimal Discount,
    decimal LineTotal,
    int? AvailableStock,
    bool? IsActiveInCatalog,
    double? AverageProductRating,
    int ReviewsCount);

/// <summary>
/// Зведена аналітика за агрегованою карткою угоди/замовлення
/// </summary>
public record AggregationSummaryDto(
    int TotalDistinctProducts,
    int CatalogProductsMatched,
    int TotalReviewsCount,
    double? AverageRating,
    int TotalClientActivitiesCount,
    bool IsPartialResponse);

/// <summary>
/// Композитний DTO повної картки замовлення/угоди (Aggregator Pattern):
/// об'єднує дані з трьох мікросервісів (Sales/Orders, Catalog, Activities/Reviews) та перелік попереджень при часткових відмовах.
/// </summary>
public record FullOrderDto(
    OrderDto Order,
    IReadOnlyList<ProductDto> Products,
    IReadOnlyList<ReviewDto> Reviews,
    IReadOnlyList<ActivityDto>? Activities,
    IReadOnlyList<EnrichedOrderItemDto> EnrichedItems,
    AggregationSummaryDto Summary,
    IReadOnlyList<string> Warnings);
