using Activities.Application.Common.Interfaces;
using Activities.Application.Common.Models;

namespace Activities.Application.Activities.Queries.GetActivityById;

/// <summary>
/// Запит отримання активності/відгуку за Id (не кешується для гарантії актуальності ETag/Version)
/// </summary>
public record GetActivityByIdQuery(string Id) : IQuery<ActivityDto>;
