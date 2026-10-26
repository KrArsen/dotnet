using FluentValidation;

namespace Activities.Application.Activities.Queries.GetActivitiesList;

public class GetActivitiesListQueryValidator : AbstractValidator<GetActivitiesListQuery>
{
    public GetActivitiesListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Номер сторінки (Page) має бути не меншим за 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Розмір сторінки (PageSize) має бути в межах від 1 до 100.");

        When(x => x.ClientId.HasValue, () =>
        {
            RuleFor(x => x.ClientId!.Value)
                .GreaterThan(0).WithMessage("ClientId повинен бути більшим за 0.");
        });

        When(x => x.ProductId.HasValue, () =>
        {
            RuleFor(x => x.ProductId!.Value)
                .GreaterThan(0).WithMessage("ProductId повинен бути більшим за 0.");
        });
    }
}
