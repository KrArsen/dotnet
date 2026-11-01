using System.Diagnostics;
using Activities.Domain.Exceptions;
using Activities.Domain.Interfaces;
using Activities.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using DomainActivity = Activities.Domain.Entities.Activity;

namespace Activities.Application.Activities.Commands.UpdateActivity;

public class UpdateActivityCommandHandler : IRequestHandler<UpdateActivityCommand>
{
    private static readonly ActivitySource Source = new("Activities.WebApi");

    private readonly IActivityRepository _repository;
    private readonly ILogger<UpdateActivityCommandHandler> _logger;

    public UpdateActivityCommandHandler(
        IActivityRepository repository,
        ILogger<UpdateActivityCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(UpdateActivityCommand request, CancellationToken cancellationToken)
    {
        using var span = Source.StartActivity("UpdateActivity");
        span?.SetTag("activity.id", request.Id);
        span?.SetTag("activity.expectedVersion", request.ExpectedVersion);

        var objectId = ObjectId.Parse(request.Id);
        var activity = await _repository.GetByIdAsync(objectId, cancellationToken);

        if (activity == null)
        {
            throw new NotFoundException(nameof(DomainActivity), request.Id);
        }

        var rating = request.Rating.HasValue ? new Rating(request.Rating.Value) : null;
        activity.Update(request.Text, rating, request.Outcome);

        await _repository.UpdateAsync(activity, request.ExpectedVersion, cancellationToken);

        _logger.LogInformation(
            "Activity {ActivityId} updated to Version {Version}, Rating {Rating}, Status {Status}",
            request.Id,
            activity.Version,
            request.Rating,
            "Updated");
    }
}
