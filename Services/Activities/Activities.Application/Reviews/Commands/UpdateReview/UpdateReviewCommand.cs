using Activities.Application.Common.Interfaces;
using Activities.Domain.Entities;
using Activities.Domain.Exceptions;
using Activities.Domain.Interfaces;
using Activities.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using MongoDB.Bson;

namespace Activities.Application.Reviews.Commands.UpdateReview;

public record UpdateReviewCommand(
    string Id,
    string Text,
    int Rating,
    int ExpectedVersion = 1) : ICommand;

public class UpdateReviewCommandHandler : IRequestHandler<UpdateReviewCommand>
{
    private readonly IActivityRepository _repository;

    public UpdateReviewCommandHandler(IActivityRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task Handle(UpdateReviewCommand request, CancellationToken cancellationToken)
    {
        var objectId = ObjectId.Parse(request.Id);
        var existing = await _repository.GetByIdAsync(objectId, cancellationToken);
        if (existing == null)
        {
            throw new NotFoundException(nameof(Review), request.Id);
        }

        existing.Update(request.Text, new Rating(request.Rating));
        await _repository.UpdateAsync(existing, request.ExpectedVersion, cancellationToken);
    }
}

public class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(id => ObjectId.TryParse(id, out _)).WithMessage("Id must be a valid ObjectId.");
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
    }
}
