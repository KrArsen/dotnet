using Activities.Domain.Entities;
using Activities.Domain.Exceptions;
using Activities.Domain.Interfaces;
using Activities.Domain.ValueObjects;
using MediatR;
using MongoDB.Bson;

namespace Activities.Application.Activities.Commands.UpdateActivity;

public class UpdateActivityCommandHandler : IRequestHandler<UpdateActivityCommand>
{
    private readonly IActivityRepository _repository;

    public UpdateActivityCommandHandler(IActivityRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task Handle(UpdateActivityCommand request, CancellationToken cancellationToken)
    {
        var objectId = ObjectId.Parse(request.Id);
        var activity = await _repository.GetByIdAsync(objectId, cancellationToken);

        if (activity == null)
        {
            throw new NotFoundException(nameof(Activity), request.Id);
        }

        var rating = request.Rating.HasValue ? new Rating(request.Rating.Value) : null;
        activity.Update(request.Text, rating, request.Outcome);

        await _repository.UpdateAsync(activity, request.ExpectedVersion, cancellationToken);
    }
}
