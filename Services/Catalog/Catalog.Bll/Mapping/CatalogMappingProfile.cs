using AutoMapper;
using Catalog.Bll.DTOs;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Mapping;

public class CatalogMappingProfile : Profile
{
    public CatalogMappingProfile()
    {
        CreateMap<Category, CategoryDto>();

        CreateMap<Category, CategoryWithProductsDto>()
            .ForMember(dest => dest.Products, opt => opt.MapFrom(src => src.ProductCategories.Select(pc => pc.Product)));

        CreateMap<ProductDetail, ProductDetailDto>();

        CreateMap<ProductImage, ProductImageDto>();

        CreateMap<Product, ProductDto>()
            .ForMember(dest => dest.Categories, opt => opt.MapFrom(src => src.ProductCategories.Select(pc => pc.Category)));
    }
}
