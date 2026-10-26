namespace Catalog.Bll.DTOs;

public record ProductDto(
    long Id,
    string Sku,
    string Name,
    string ItemType,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    ProductDetailDto? Detail,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<CategoryDto> Categories);

public record CreateProductDto(
    string Sku,
    string Name,
    string ItemType,
    decimal Price,
    int StockQuantity,
    string? Description,
    string? Unit,
    int? DurationMinutes,
    decimal? TaxRate,
    List<long>? CategoryIds);

public record UpdatePriceDto(
    decimal NewPrice,
    string? Reason);

public record RemoveStockDto(
    int Quantity);

public record AddStockDto(
    int Quantity);

public record ProductDetailDto(
    long ProductId,
    string Description,
    string Unit,
    int? DurationMinutes,
    decimal TaxRate);

public record ProductImageDto(
    long Id,
    string Url,
    string? AltText,
    bool IsMain,
    int SortOrder);
