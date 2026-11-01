using System.Diagnostics;
using Activities.Domain.Entities;
using Activities.Domain.Interfaces;
using Activities.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Activities.Application.Reviews.Commands.CreateReview;

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, string>
{
    private static readonly ActivitySource Source = new("Activities.WebApi");

    private readonly IActivityRepository _repository;
    private readonly ILogger<CreateReviewCommandHandler> _logger;

    public CreateReviewCommandHandler(
        IActivityRepository repository,
        ILogger<CreateReviewCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        using var span = Source.StartActivity("CreateReview");
        span?.SetTag("review.productId", request.ProductId);
        span?.SetTag("review.authorId", request.AuthorUserId);
        span?.SetTag("review.rating", request.Rating);

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

        var reviewId = review.Id.ToString();
        span?.SetTag("review.id", reviewId);

        _logger.LogInformation(
            "Review {ReviewId} created for Product {ProductId} by User {UserId}, Rating {Rating}, Status {Status}",
            reviewId,
            request.ProductId,
            request.AuthorUserId,
            request.Rating,
            "Created");

        return reviewId;
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
