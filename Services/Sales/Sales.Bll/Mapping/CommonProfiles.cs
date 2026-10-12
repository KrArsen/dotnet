using AutoMapper;
using Sales.Bll.Dto;
using Sales.Domain.Models;

namespace Sales.Bll.Mapping;

public class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Product, ProductDto>()
            .ConstructUsing(src => new ProductDto(
                src.Id,
                src.Name,
                src.Price,
                src.IsActive,
                src.CreatedAt,
                src.CreatedBy,
                src.UpdatedAt,
                src.UpdatedBy));
    }
}

public class TeamMemberProfile : Profile
{
    public TeamMemberProfile()
    {
        CreateMap<TeamMember, TeamMemberDto>()
            .ConstructUsing(src => new TeamMemberDto(
                src.Id,
                src.FullName,
                src.Email,
                src.Role,
                src.IsActive,
                src.CreatedAt,
                src.CreatedBy,
                src.UpdatedAt,
                src.UpdatedBy));
    }
}
