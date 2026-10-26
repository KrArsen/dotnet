using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.DTOs;

public record CategoryDto(
    long Id,
    string Name,
    string? Description);

public record CreateCategoryDto(
    [property: Required(ErrorMessage = "Назва категорії є обов'язковою.")]
    [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Довжина назви категорії має бути від 2 до 100 символів.")]
    string Name,

    [property: StringLength(500, ErrorMessage = "Опис категорії не може перевищувати 500 символів.")]
    string? Description);

public record CategoryWithProductsDto(
    long Id,
    string Name,
    string? Description,
    IReadOnlyList<ProductDto> Products);
