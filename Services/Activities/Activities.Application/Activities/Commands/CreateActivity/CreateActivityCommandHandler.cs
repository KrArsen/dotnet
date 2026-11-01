using System.Diagnostics;
using Activities.Domain.Interfaces;
using Activities.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using DomainActivity = Activities.Domain.Entities.Activity;

namespace Activities.Application.Activities.Commands.CreateActivity;

public class CreateActivityCommandHandler : IRequestHandler<CreateActivityCommand, string>
{
    private static readonly ActivitySource Source = new("Activities.WebApi");

    private readonly IActivityRepository _repository;
    private readonly ILogger<CreateActivityCommandHandler> _logger;

    public CreateActivityCommandHandler(
        IActivityRepository repository,
        ILogger<CreateActivityCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> Handle(CreateActivityCommand request, CancellationToken cancellationToken)
    {
        using var span = Source.StartActivity("CreateActivity");
        span?.SetTag("activity.clientId", request.ClientId);
        span?.SetTag("activity.type", request.Type);
        span?.SetTag("activity.authorId", request.AuthorUserId);

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

        var activity = new DomainActivity(
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

        var activityId = activity.Id.ToString();
        span?.SetTag("activity.id", activityId);

        _logger.LogInformation(
            "Activity {ActivityId} created for Client {ClientId} by User {UserId}, Type {Type}, Rating {Rating}, Status {Status}",
            activityId,
            request.ClientId,
            request.AuthorUserId,
            request.Type,
            request.Rating,
            "Created");

        return activityId;
    }
}
