using Activities.Application.Common.Caching;
using Activities.Application.Common.Interfaces;
using Activities.Application.Common.Models;
using Activities.Domain.Entities;
using Activities.Domain.Exceptions;
using FluentValidation;
using MediatR;
using MongoDB.Bson;

namespace Activities.Application.Reviews.Queries.GetReviewById;

public record GetReviewByIdQuery(string Id) : IQuery<ReviewDto>;

public class GetReviewByIdQueryHandler : IRequestHandler<GetReviewByIdQuery, ReviewDto>
{
    private readonly IReviewQueries _queries;

    public GetReviewByIdQueryHandler(IReviewQueries queries)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<ReviewDto> Handle(GetReviewByIdQuery request, CancellationToken cancellationToken)
    {
        var objectId = ObjectId.Parse(request.Id);
        var dto = await _queries.GetReviewByIdAsync(objectId, cancellationToken);
        if (dto == null)
        {
            throw new NotFoundException(nameof(Review), request.Id);
        }
        return dto;
    }
}

public class GetReviewByIdQueryValidator : AbstractValidator<GetReviewByIdQuery>
{
    public GetReviewByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(id => ObjectId.TryParse(id, out _)).WithMessage("Id must be a valid ObjectId.");
    }
}
