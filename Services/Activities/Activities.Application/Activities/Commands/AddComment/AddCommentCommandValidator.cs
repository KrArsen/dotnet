using FluentValidation;
using MongoDB.Bson;

namespace Activities.Application.Activities.Commands.AddComment;

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.ActivityId)
            .NotEmpty().WithMessage("ActivityId є обов'язковим.")
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Id must be a valid ObjectId.");

        RuleFor(x => x.Author)
            .NotEmpty().WithMessage("Автор коментаря є обов'язковим.")
            .MaximumLength(100).WithMessage("Ім'я автора не може перевищувати 100 символів.");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Текст коментаря є обов'язковим.")
            .MaximumLength(2000).WithMessage("Текст коментаря не може перевищувати 2000 символів.");
    }
}
