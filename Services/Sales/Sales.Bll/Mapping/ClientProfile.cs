using AutoMapper;
using Sales.Bll.Dto;
using Sales.Domain.Models;

namespace Sales.Bll.Mapping;

public class ClientProfile : Profile
{
    public ClientProfile()
    {
        CreateMap<Client, ClientDto>()
            .ConstructUsing(src => new ClientDto(
                src.Id,
                src.CompanyName,
                src.ContactPerson,
                src.Email,
                src.Phone,
                src.Industry,
                src.CreatedAt,
                src.CreatedBy,
                src.UpdatedAt,
                src.UpdatedBy,
                src.RowVersion != null ? Convert.ToBase64String(src.RowVersion) : string.Empty));

        CreateMap<CreateClientDto, Client>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore());

        CreateMap<UpdateClientDto, Client>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.MapFrom(src =>
                !string.IsNullOrWhiteSpace(src.RowVersion) ? Convert.FromBase64String(src.RowVersion) : null));
    }
}
