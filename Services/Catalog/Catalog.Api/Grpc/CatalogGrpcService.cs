using Catalog.Bll.Services;
using Catalog.Domain.Exceptions;
using Catalog.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Catalog.Api.Grpc;

/// <summary>
/// gRPC-сервіс каталогу, який виступає тонким адаптером над BLL (IProductService із дворівневим кешем L1+L2).
/// Перехоплює доменні винятки та повертає коректні коди статусу gRPC (InvalidArgument, NotFound).
/// </summary>
public sealed class CatalogGrpcService : CatalogService.CatalogServiceBase
{
    private readonly IProductService _products;
    private readonly ILogger<CatalogGrpcService> _logger;

    public CatalogGrpcService(IProductService products, ILogger<CatalogGrpcService> logger)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Product Id must be positive"));
        }

        try
        {
            var dto = await _products.GetByIdAsync(request.Id, context.CancellationToken);
            return MapToReply(dto);
        }
        catch (EntityNotFoundException ex)
        {
            _logger.LogWarning("gRPC GetProduct: product {ProductId} not found ({Message})", request.Id, ex.Message);
            throw new RpcException(new Status(StatusCode.NotFound, $"Product {request.Id} not found"));
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<ProductsReply> GetProductsByIds(GetProductsByIdsRequest request, ServerCallContext context)
    {
        if (request.Ids is null || request.Ids.Count == 0)
        {
            return new ProductsReply();
        }

        var distinctIds = request.Ids.Where(id => id > 0).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "At least one positive Product Id is required"));
        }

        var reply = new ProductsReply();

        foreach (var id in distinctIds)
        {
            try
            {
                var dto = await _products.GetByIdAsync(id, context.CancellationToken);
                reply.Items.Add(MapToReply(dto));
            }
            catch (EntityNotFoundException)
            {
                _logger.LogDebug("gRPC GetProductsByIds: product {ProductId} not found, skipping", id);
            }
        }

        return reply;
    }

    private static ProductReply MapToReply(Catalog.Bll.DTOs.ProductDto dto) =>
        new()
        {
            Id = dto.Id,
            Sku = dto.Sku ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            ItemType = dto.ItemType ?? string.Empty,
            Price = (double)dto.Price,
            StockQuantity = dto.StockQuantity,
            IsActive = dto.IsActive,
            Description = dto.Detail?.Description ?? string.Empty,
            Unit = dto.Detail?.Unit ?? "шт."
        };
}
