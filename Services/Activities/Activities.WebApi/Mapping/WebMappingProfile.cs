using Activities.Application.Activities.Commands.CreateActivity;
using Activities.Application.Activities.Commands.UpdateActivity;
using Activities.Application.Reviews.Commands.CreateReview;
using Activities.Application.Reviews.Commands.UpdateReview;
using Activities.WebApi.Contracts;
using AutoMapper;

namespace Activities.WebApi.Mapping;

public class WebMappingProfile : Profile
{
    public WebMappingProfile()
    {
        CreateMap<CreateActivityRequest, CreateActivityCommand>();
        CreateMap<CreateReviewRequest, CreateReviewCommand>();
    }
}
