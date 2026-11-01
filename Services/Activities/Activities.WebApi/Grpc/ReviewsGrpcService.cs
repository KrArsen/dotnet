using System.Globalization;
using Activities.Application.Activities.Queries.GetActivitiesList;
using Activities.Application.Reviews.Queries.GetReviewsList;
using Activities.Domain.Exceptions;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Grpc;

namespace Activities.WebApi.Grpc;

/// <summary>
/// gRPC-сервіс відгуків та активностей клієнтів (адаптер над CQRS/MediatR ISender).
/// Виконує валідацію параметрів і мапить доменні винятки на StatusCode.InvalidArgument / StatusCode.NotFound.
/// </summary>
public sealed class ReviewsGrpcService : ReviewsService.ReviewsServiceBase
{
    private readonly ISender _sender;
    private readonly ILogger<ReviewsGrpcService> _logger;

    public ReviewsGrpcService(ISender sender, ILogger<ReviewsGrpcService> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<ReviewsReply> GetForProducts(GetForProductsRequest request, ServerCallContext context)
    {
        if (request.ProductIds is null || request.ProductIds.Count == 0)
        {
            return new ReviewsReply();
        }

        var distinctIds = request.ProductIds.Where(id => id > 0).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "At least one positive ProductId is required"));
        }

        var reply = new ReviewsReply();

        try
        {
            foreach (var productId in distinctIds)
            {
                var page = await _sender.Send(
                    new GetReviewsListQuery(productId, Page: 1, PageSize: 20),
                    context.CancellationToken);

                foreach (var item in page.Items)
                {
                    reply.Items.Add(new ReviewItemReply
                    {
                        Id = item.Id ?? string.Empty,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName ?? string.Empty,
                        AuthorName = item.AuthorName ?? string.Empty,
                        Rating = item.Rating,
                        Text = item.Text ?? string.Empty,
                        CreatedAt = item.CreatedAt.ToString("O", CultureInfo.InvariantCulture)
                    });
                }
            }

            return reply;
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("gRPC GetForProducts not found: {Message}", ex.Message);
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<ActivitiesReply> GetActivitiesByClient(
        GetActivitiesByClientRequest request,
        ServerCallContext context)
    {
        if (request.ClientId <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ClientId must be positive"));
        }

        var pageSize = request.PageSize <= 0 ? 20 : Math.Clamp(request.PageSize, 1, 100);

        try
        {
            var page = await _sender.Send(
                new GetActivitiesListQuery(
                    ClientId: request.ClientId,
                    Type: null,
                    UserId: null,
                    Search: null,
                    ProductId: null,
                    Page: 1,
                    PageSize: pageSize),
                context.CancellationToken);

            var reply = new ActivitiesReply();
            foreach (var item in page.Items)
            {
                reply.Items.Add(new ActivityItemReply
                {
                    Id = item.Id ?? string.Empty,
                    Type = item.Type ?? string.Empty,
                    AuthorName = item.AuthorName ?? string.Empty,
                    Text = item.Text ?? string.Empty,
                    Rating = item.Rating ?? 0,
                    Outcome = item.Outcome ?? string.Empty,
                    CreatedAt = item.CreatedAt.ToString("O", CultureInfo.InvariantCulture)
                });
            }

            return reply;
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }
}
