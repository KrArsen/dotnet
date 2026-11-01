using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Sales.Api.Grpc;

/// <summary>
/// Серверний gRPC Interceptor для структурованого логування викликів OrdersService.
/// </summary>
public sealed class OrdersLoggingInterceptor : Interceptor
{
    private readonly ILogger<OrdersLoggingInterceptor> _logger;

    public OrdersLoggingInterceptor(ILogger<OrdersLoggingInterceptor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await continuation(request, context);
            sw.Stop();
            _logger.LogInformation(
                "gRPC {Method} from {Peer}: {Status} in {Elapsed} ms",
                context.Method,
                context.Peer,
                StatusCode.OK,
                sw.ElapsedMilliseconds);
            return response;
        }
        catch (RpcException ex)
        {
            sw.Stop();
            _logger.LogWarning(
                ex,
                "gRPC {Method} from {Peer} failed: {Status} ({Detail}) in {Elapsed} ms",
                context.Method,
                context.Peer,
                ex.StatusCode,
                ex.Status.Detail,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
