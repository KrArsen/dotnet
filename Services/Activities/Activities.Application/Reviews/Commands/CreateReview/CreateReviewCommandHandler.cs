using Activities.Domain.Entities;
using Activities.Domain.Interfaces;
using Activities.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Activities.Application.Reviews.Commands.CreateReview;

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, string>
{
    private readonly IActivityRepository _repository;

    public CreateReviewCommandHandler(IActivityRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<string> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        var author = new Author(request.AuthorUserId, request.AuthorDisplayName);
        var rating = new Rating(request.Rating);
        var product = new RelatedProduct(request.ProductId, request.ProductName);

        var review = new Review(
            clientId: request.ProductId,
            clientName: request.ProductName,
            author: author,
            text: request.Text,
            rating: rating,
            relatedProducts: new[] { product });

        await _repository.AddAsync(review, cancellationToken);
        return review.Id.ToString();
    }
}

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator(IActivityRepository repository)
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("ProductId повинен бути більшим за 0.");
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AuthorUserId).GreaterThan(0);
        RuleFor(x => x.AuthorDisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).WithMessage("Rating має бути від 1 до 5.");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await repository.ExistsAsync(cmd.ProductId, cmd.AuthorUserId, ct))
            .WithMessage("Користувач уже залишив відгук на цей товар/послугу.")
            .WithName("AuthorUserId");
    }
}
