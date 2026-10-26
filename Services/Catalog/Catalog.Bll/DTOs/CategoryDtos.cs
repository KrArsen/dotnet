namespace Catalog.Bll.DTOs;

public record CategoryDto(
    long Id,
    string Name,
    string? Description);

public record CreateCategoryDto(
    string Name,
    string? Description);

public record CategoryWithProductsDto(
    long Id,
    string Name,
    string? Description,
    IReadOnlyList<ProductDto> Products);
