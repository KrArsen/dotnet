using FluentValidation;
using MongoDB.Bson;

namespace Activities.Application.Activities.Commands.UpdateActivity;

public class UpdateActivityCommandValidator : AbstractValidator<UpdateActivityCommand>
{
    public UpdateActivityCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id є обов'язковим.")
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Id must be a valid ObjectId.");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Текст є обов'язковим.")
            .MaximumLength(5000).WithMessage("Текст не може перевищувати 5000 символів.");

        When(x => x.Rating.HasValue, () =>
        {
            RuleFor(x => x.Rating!.Value)
                .InclusiveBetween(1, 5).WithMessage("Рейтинг має становити від 1 до 5.");
        });

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Очікувана версія (ExpectedVersion) має бути більшою за 0.");
    }
}
