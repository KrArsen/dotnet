using System.ComponentModel.DataAnnotations;

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
    [property: Required(ErrorMessage = "Артикул (SKU) є обов'язковим.")]
    [property: StringLength(50, MinimumLength = 3, ErrorMessage = "Довжина артикулу має становити від 3 до 50 символів.")]
    [property: RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Артикул має містити лише латинські літери, цифри, дефіс або підкреслення.")]
    string Sku,

    [property: Required(ErrorMessage = "Назва позиції каталогу є обов'язковою.")]
    [property: StringLength(200, MinimumLength = 3, ErrorMessage = "Довжина назви має бути від 3 до 200 символів.")]
    string Name,

    [property: Required(ErrorMessage = "Тип позиції є обов'язковим.")]
    string ItemType,

    [property: Range(0.01, 100000000.0, ErrorMessage = "Ціна має бути строго більшою за 0.")]
    decimal Price,

    [property: Range(0, 1000000, ErrorMessage = "Кількість на складі не може бути від'ємною.")]
    int StockQuantity,

    [property: StringLength(2000, ErrorMessage = "Опис не може перевищувати 2000 символів.")]
    string? Description,

    [property: StringLength(20, ErrorMessage = "Одиниця виміру не може перевищувати 20 символів.")]
    string? Unit,

    [property: Range(1, 100000, ErrorMessage = "Тривалість у хвилинах має бути більшою за 0.")]
    int? DurationMinutes,

    [property: Range(0.0, 100.0, ErrorMessage = "Ставка податку має бути в межах від 0% до 100%.")]
    decimal? TaxRate,

    List<long>? CategoryIds);

public record UpdatePriceDto(
    [property: Range(0.01, 100000000.0, ErrorMessage = "Нова ціна має бути строго більшою за 0.")]
    decimal NewPrice,

    [property: StringLength(500, ErrorMessage = "Причина зміни ціни не може перевищувати 500 символів.")]
    string? Reason);

public record RemoveStockDto(
    [property: Range(1, 1000000, ErrorMessage = "Кількість для списання повинна бути більшою за 0.")]
    int Quantity);

public record AddStockDto(
    [property: Range(1, 1000000, ErrorMessage = "Кількість для поповнення повинна бути більшою за 0.")]
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
