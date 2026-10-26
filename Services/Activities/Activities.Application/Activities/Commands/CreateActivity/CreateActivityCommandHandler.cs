using Activities.Domain.Entities;
using Activities.Domain.Interfaces;
using Activities.Domain.ValueObjects;
using MediatR;

namespace Activities.Application.Activities.Commands.CreateActivity;

public class CreateActivityCommandHandler : IRequestHandler<CreateActivityCommand, string>
{
    private readonly IActivityRepository _repository;

    public CreateActivityCommandHandler(IActivityRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<string> Handle(CreateActivityCommand request, CancellationToken cancellationToken)
    {
        var author = new Author(request.AuthorUserId, request.AuthorDisplayName);
        var rating = request.Rating.HasValue ? new Rating(request.Rating.Value) : null;

        var relatedProducts = new List<RelatedProduct>();
        if (request.ProductIds != null)
        {
            foreach (var pid in request.ProductIds)
            {
                relatedProducts.Add(new RelatedProduct(pid, $"Послуга #{pid}"));
            }
        }

        var activity = new Activity(
            type: request.Type,
            clientId: request.ClientId,
            clientName: request.ClientName,
            author: author,
            text: request.Text,
            rating: rating,
            dealId: request.DealId,
            dealTitle: request.DealTitle,
            direction: request.Direction,
            durationSec: request.DurationSec,
            outcome: request.Outcome,
            subject: request.Subject,
            attachments: request.Attachments,
            location: request.Location,
            participants: request.Participants,
            agenda: request.Agenda,
            dueDate: request.DueDate,
            relatedProducts: relatedProducts.Count > 0 ? relatedProducts : null);

        await _repository.AddAsync(activity, cancellationToken);

        return activity.Id.ToString();
    }
}
