using AutoMapper;
using Sales.Bll.Dto;
using Sales.Domain.Models;

namespace Sales.Bll.Mapping;

public class DealProfile : Profile
{
    public DealProfile()
    {
        CreateMap<DealDetails, DealDetailsDto>()
            .ConstructUsing(src => new DealDetailsDto(
                src.DealId,
                src.Source,
                src.Description,
                src.LostReason,
                src.ExpectedCloseDate,
                src.CreatedAt,
                src.CreatedBy,
                src.UpdatedAt,
                src.UpdatedBy));

        CreateMap<DealItem, DealItemDto>()
            .ConstructUsing(src => new DealItemDto(
                src.DealId,
                src.ProductId,
                src.ProductName,
                src.UnitPrice,
                src.Discount,
                src.Quantity,
                src.TotalPrice,
                src.CreatedAt,
                src.CreatedBy));

        CreateMap<DealStatusHistoryEntry, DealStatusHistoryDto>()
            .ConstructUsing(src => new DealStatusHistoryDto(
                src.Id,
                src.DealId,
                src.OldStatus,
                src.NewStatus,
                src.ChangedAt,
                src.ChangedBy,
                src.Reason));

        CreateMap<Deal, DealDto>()
            .ConstructUsing((src, ctx) => new DealDto(
                src.Id,
                src.DealNumber,
                src.Title,
                src.ClientId,
                src.OwnerId,
                src.Stage,
                src.TotalAmount,
                src.CreatedAt,
                src.CreatedBy,
                src.UpdatedAt,
                src.UpdatedBy,
                ctx.Mapper.Map<DealDetailsDto?>(src.Details),
                ctx.Mapper.Map<IReadOnlyList<DealItemDto>>(src.Items),
                ctx.Mapper.Map<IReadOnlyList<DealStatusHistoryDto>>(src.History),
                ctx.Mapper.Map<ClientDto?>(src.Client),
                ctx.Mapper.Map<TeamMemberDto?>(src.Owner)));

        CreateMap<DealSummary, DealSummaryDto>()
            .ConstructUsing(src => new DealSummaryDto(
                src.DealId,
                src.DealNumber,
                src.Title,
                src.Stage,
                src.TotalAmount,
                src.ClientId,
                src.ClientCompanyName,
                src.ClientContactPerson,
                src.OwnerId,
                src.OwnerName,
                src.DealSource,
                src.ExpectedCloseDate,
                src.LostReason,
                src.CreatedAt,
                src.UpdatedAt,
                src.ItemsCount,
                src.CalculatedTotal));

        CreateMap<DealStatusStat, DealStatsDto>()
            .ConstructUsing(src => new DealStatsDto(
                src.Stage,
                src.DealsCount,
                src.TotalAmount,
                src.AverageAmount));
    }
}
