using Activities.Domain.Entities;
using Activities.Domain.Exceptions;
using Activities.Domain.Interfaces;
using MediatR;
using MongoDB.Bson;

namespace Activities.Application.Activities.Commands.DeleteActivity;

public class DeleteActivityCommandHandler : IRequestHandler<DeleteActivityCommand>
{
    private readonly IActivityRepository _repository;

    public DeleteActivityCommandHandler(IActivityRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task Handle(DeleteActivityCommand request, CancellationToken cancellationToken)
    {
        var objectId = ObjectId.Parse(request.Id);
        var existing = await _repository.GetByIdAsync(objectId, cancellationToken);

        if (existing == null)
        {
            throw new NotFoundException(nameof(Activity), request.Id);
        }

        await _repository.DeleteAsync(objectId, cancellationToken);
    }
}
