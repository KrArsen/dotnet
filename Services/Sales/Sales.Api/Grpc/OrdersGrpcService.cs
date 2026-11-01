using System.Globalization;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Orders.Grpc;
using Sales.Bll.Services;
using Sales.Domain.Exceptions;

namespace Sales.Api.Grpc;

/// <summary>
/// gRPC-сервіс замовлень та клієнтів (адаптер над IDealService та IClientService).
/// Забезпечує валідацію вхідних даних і коректні коди статусу gRPC (InvalidArgument, NotFound).
/// </summary>
public sealed class OrdersGrpcService : OrdersService.OrdersServiceBase
{
    private readonly IDealService _deals;
    private readonly IClientService _clients;
    private readonly ILogger<OrdersGrpcService> _logger;

    public OrdersGrpcService(
        IDealService deals,
        IClientService clients,
        ILogger<OrdersGrpcService> logger)
    {
        _deals = deals ?? throw new ArgumentNullException(nameof(deals));
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<OrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Order Id must be positive"));
        }

        try
        {
            var deal = await _deals.GetByIdAsync(request.Id, context.CancellationToken);
            if (deal is null)
            {
                _logger.LogWarning("gRPC GetOrder: order {OrderId} not found", request.Id);
                throw new RpcException(new Status(StatusCode.NotFound, $"Order {request.Id} not found"));
            }

            var reply = new OrderReply
            {
                Id = deal.Id,
                DealNumber = deal.DealNumber ?? $"DEAL-{deal.Id}",
                Title = deal.Title ?? string.Empty,
                Status = deal.Stage ?? "New",
                TotalAmount = (double)deal.TotalAmount,
                CreatedAt = deal.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                ClientId = deal.ClientId,
                OwnerName = deal.Owner?.FullName ?? string.Empty,
                OwnerRole = deal.Owner?.Role ?? string.Empty
            };

            if (deal.Client is not null)
            {
                reply.Client = new ClientReply
                {
                    Id = deal.Client.Id,
                    CompanyName = deal.Client.CompanyName ?? string.Empty,
                    ContactPerson = deal.Client.ContactPerson ?? string.Empty,
                    Email = deal.Client.Email ?? string.Empty,
                    Phone = deal.Client.Phone ?? string.Empty,
                    Industry = deal.Client.Industry ?? string.Empty
                };
            }

            if (deal.Items is not null)
            {
                foreach (var item in deal.Items)
                {
                    reply.Items.Add(new OrderItemReply
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName ?? string.Empty,
                        Quantity = item.Quantity,
                        UnitPrice = (double)item.UnitPrice,
                        Discount = (double)item.Discount,
                        TotalPrice = (double)item.TotalPrice
                    });
                }
            }

            return reply;
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<ClientReply> GetClient(GetClientRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Client Id must be positive"));
        }

        var client = await _clients.GetByIdAsync(request.Id, context.CancellationToken);
        if (client is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Client {request.Id} not found"));
        }

        return new ClientReply
        {
            Id = client.Id,
            CompanyName = client.CompanyName ?? string.Empty,
            ContactPerson = client.ContactPerson ?? string.Empty,
            Email = client.Email ?? string.Empty,
            Phone = client.Phone ?? string.Empty,
            Industry = client.Industry ?? string.Empty
        };
    }
}
