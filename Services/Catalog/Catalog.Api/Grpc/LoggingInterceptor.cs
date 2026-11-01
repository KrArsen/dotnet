using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Catalog.Api.Grpc;

/// <summary>
/// Серверний gRPC Interceptor для структурованого логування методу, клієнта (Peer), статусу та тривалості.
/// </summary>
public sealed class CatalogLoggingInterceptor : Interceptor
{
    private readonly ILogger<CatalogLoggingInterceptor> _logger;

    public CatalogLoggingInterceptor(ILogger<CatalogLoggingInterceptor> logger)
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
