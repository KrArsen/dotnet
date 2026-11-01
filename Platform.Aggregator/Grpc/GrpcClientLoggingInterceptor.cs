using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Platform.Aggregator.Grpc;

/// <summary>
/// Interceptor gRPC-клієнта в Aggregator для логування вихідних gRPC-викликів (Method, Peer/Host, Status, Elapsed)
/// та передачі заголовка X-Correlation-Id у метаданих gRPC.
/// </summary>
public sealed class GrpcClientLoggingInterceptor : Interceptor
{
    private readonly ILogger<GrpcClientLoggingInterceptor> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GrpcClientLoggingInterceptor(
        ILogger<GrpcClientLoggingInterceptor> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        var correlationId = _httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.HeaderName]?.ToString()
            ?? Activity.Current?.TraceId.ToString();

        if (!string.IsNullOrWhiteSpace(correlationId) &&
            headers.GetValue("x-correlation-id") is null)
        {
            headers.Add("x-correlation-id", correlationId);
        }

        var options = context.Options.WithHeaders(headers);
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);

        var sw = Stopwatch.StartNew();
        var call = continuation(request, newContext);

        return new AsyncUnaryCall<TResponse>(
            HandleResponseAsync(call.ResponseAsync, context.Method.FullName, context.Host ?? "unknown", sw),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private async Task<TResponse> HandleResponseAsync<TResponse>(
        Task<TResponse> responseTask,
        string method,
        string peer,
        Stopwatch sw)
    {
        try
        {
            var response = await responseTask;
            sw.Stop();
            _logger.LogInformation(
                "gRPC client {Method} to {Peer}: {Status} in {Elapsed} ms",
                method,
                peer,
                StatusCode.OK,
                sw.ElapsedMilliseconds);
            return response;
        }
        catch (RpcException ex)
        {
            sw.Stop();
            _logger.LogWarning(
                ex,
                "gRPC client {Method} to {Peer} failed: {Status} ({Detail}) in {Elapsed} ms",
                method,
                peer,
                ex.StatusCode,
                ex.Status.Detail,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
