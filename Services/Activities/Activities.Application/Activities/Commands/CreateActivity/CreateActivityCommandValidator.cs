using Activities.Domain.Interfaces;
using FluentValidation;

namespace Activities.Application.Activities.Commands.CreateActivity;

public class CreateActivityCommandValidator : AbstractValidator<CreateActivityCommand>
{
    public CreateActivityCommandValidator(IActivityRepository repository)
    {
        RuleFor(x => x.ClientId)
            .GreaterThan(0).WithMessage("Ідентифікатор клієнта повинен бути більшим за 0.");

        RuleFor(x => x.ClientName)
            .NotEmpty().WithMessage("Назва клієнта є обов'язковою.")
            .MaximumLength(200).WithMessage("Назва клієнта не може перевищувати 200 символів.");

        RuleFor(x => x.AuthorUserId)
            .GreaterThan(0).WithMessage("Ідентифікатор автора повинен бути більшим за 0.");

        RuleFor(x => x.AuthorDisplayName)
            .NotEmpty().WithMessage("Ім'я автора є обов'язковим.")
            .MaximumLength(100).WithMessage("Ім'я автора не може перевищувати 100 символів.");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Текст активності/відгуку є обов'язковим.")
            .MaximumLength(5000).WithMessage("Текст не може перевищувати 5000 символів.");

        When(x => x.Rating.HasValue, () =>
        {
            RuleFor(x => x.Rating!.Value)
                .InclusiveBetween(1, 5).WithMessage("Рейтинг має становити від 1 до 5.");
        });

        // Асинхронне правило: заборона дублювання відгуку/активності від одного автора до одного клієнта
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                var exists = await repository.ExistsAsync(cmd.ClientId, cmd.AuthorUserId, ct);
                return !exists;
            })
            .WithMessage("Цей автор уже залишав активність або відгук для зазначеного клієнта.")
            .WithName("AuthorUserId");
    }
}
