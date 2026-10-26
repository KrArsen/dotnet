using Activities.Domain.Entities;
using Activities.Domain.Exceptions;
using Activities.Domain.Interfaces;
using MediatR;
using MongoDB.Bson;

namespace Activities.Application.Activities.Commands.AddComment;

public class AddCommentCommandHandler : IRequestHandler<AddCommentCommand>
{
    private readonly IActivityRepository _repository;

    public AddCommentCommandHandler(IActivityRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        var objectId = ObjectId.Parse(request.ActivityId);
        var activity = await _repository.GetByIdAsync(objectId, cancellationToken);

        if (activity == null)
        {
            throw new NotFoundException(nameof(Activity), request.ActivityId);
        }

        var expectedVersion = activity.Version;
        activity.AddComment(request.Author, request.Text);

        await _repository.UpdateAsync(activity, expectedVersion, cancellationToken);
    }
}
