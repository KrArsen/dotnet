using Activities.Application.Common.Models;
using Activities.Domain.Entities;
using Activities.Domain.ValueObjects;
using AutoMapper;

namespace Activities.Application.Common.Mappings;

public class ActivityMappingProfile : Profile
{
    public ActivityMappingProfile()
    {
        CreateMap<Author, AuthorDto>();
        CreateMap<RelatedProduct, RelatedProductDto>();
        CreateMap<Comment, CommentDto>();

        CreateMap<Activity, ActivityDto>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.AuthorId, opt => opt.MapFrom(s => s.Author.UserId))
            .ForMember(d => d.AuthorName, opt => opt.MapFrom(s => s.Author.DisplayName))
            .ForMember(d => d.Rating, opt => opt.MapFrom(s => s.Rating != null ? (int?)s.Rating.Value : null))
            .ForMember(d => d.CommentsCount, opt => opt.MapFrom(s => s.Comments.Count))
            .ForMember(d => d.Comments, opt => opt.MapFrom(s => s.Comments))
            .ForMember(d => d.RelatedProducts, opt => opt.MapFrom(s => s.RelatedProducts));

        CreateMap<Activity, ReviewDto>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.ProductId, opt => opt.MapFrom(s => s.RelatedProducts.Count > 0 ? s.RelatedProducts.First().ProductId : s.ClientId))
            .ForMember(d => d.ProductName, opt => opt.MapFrom(s => s.RelatedProducts.Count > 0 ? s.RelatedProducts.First().Name : s.ClientName))
            .ForMember(d => d.AuthorName, opt => opt.MapFrom(s => s.Author.DisplayName))
            .ForMember(d => d.Rating, opt => opt.MapFrom(s => s.Rating != null ? s.Rating.Value : 5))
            .ForMember(d => d.CommentsCount, opt => opt.MapFrom(s => s.Comments.Count));
    }
}
