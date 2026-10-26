using Activities.Application.Common.Interfaces;
using Activities.Application.Common.Models;
using Activities.Domain.Entities;
using Activities.Domain.Exceptions;
using MediatR;
using MongoDB.Bson;

namespace Activities.Application.Activities.Queries.GetActivityById;

public class GetActivityByIdQueryHandler : IRequestHandler<GetActivityByIdQuery, ActivityDto>
{
    private readonly IActivityQueries _queries;

    public GetActivityByIdQueryHandler(IActivityQueries queries)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<ActivityDto> Handle(GetActivityByIdQuery request, CancellationToken cancellationToken)
    {
        var objectId = ObjectId.Parse(request.Id);
        var dto = await _queries.GetByIdAsync(objectId, cancellationToken);

        if (dto == null)
        {
            throw new NotFoundException(nameof(Activity), request.Id);
        }

        return dto;
    }
}
