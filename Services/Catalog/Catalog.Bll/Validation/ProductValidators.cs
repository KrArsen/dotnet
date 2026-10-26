using Catalog.Bll.DTOs;
using FluentValidation;

namespace Catalog.Bll.Validation;

/// <summary>
/// Валідатор для створення позиції каталогу через FluentValidation
/// </summary>
public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("Артикул (SKU) є обов'язковим.")
            .Length(3, 50).WithMessage("Довжина артикулу має становити від 3 до 50 символів.")
            .Matches(@"^[A-Za-z0-9\-_]+$").WithMessage("Артикул має містити лише латинські літери, цифри, дефіс або підкреслення.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Назва позиції каталогу є обов'язковою.")
            .Length(3, 200).WithMessage("Довжина назви має бути від 3 до 200 символів.");

        RuleFor(x => x.ItemType)
            .NotEmpty().WithMessage("Тип позиції є обов'язковим.")
            .Must(x => x == "Service" || x == "Product").WithMessage("Тип елемента має бути 'Service' або 'Product'.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Ціна має бути строго більшою за 0.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Кількість на складі не може бути від'ємною.");

        When(x => x.TaxRate.HasValue, () =>
        {
            RuleFor(x => x.TaxRate!.Value)
                .InclusiveBetween(0, 100).WithMessage("Ставка податку має бути в межах від 0% до 100%.");
        });

        When(x => x.DurationMinutes.HasValue, () =>
        {
            RuleFor(x => x.DurationMinutes!.Value)
                .GreaterThan(0).WithMessage("Тривалість у хвилинах має бути більшою за 0.");
        });
    }
}

/// <summary>
/// Валідатор для зміни ціни
/// </summary>
public class UpdatePriceDtoValidator : AbstractValidator<UpdatePriceDto>
{
    public UpdatePriceDtoValidator()
    {
        RuleFor(x => x.NewPrice)
            .GreaterThan(0).WithMessage("Нова ціна має бути строго більшою за 0.");
    }
}

/// <summary>
/// Валідатор для списання залишку
/// </summary>
public class RemoveStockDtoValidator : AbstractValidator<RemoveStockDto>
{
    public RemoveStockDtoValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Кількість для списання повинна бути більшою за 0.");
    }
}

/// <summary>
/// Валідатор для поповнення залишку
/// </summary>
public class AddStockDtoValidator : AbstractValidator<AddStockDto>
{
    public AddStockDtoValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Кількість для поповнення повинна бути більшою за 0.");
    }
}

/// <summary>
/// Валідатор для створення категорії
/// </summary>
public class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Назва категорії є обов'язковою.")
            .Length(2, 100).WithMessage("Довжина назви категорії має бути від 2 до 100 символів.");
    }
}
