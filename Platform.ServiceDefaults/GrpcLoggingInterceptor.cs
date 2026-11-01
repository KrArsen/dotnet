using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Серверний gRPC Interceptor для структурованого логування вхідних gRPC-викликів:
/// логує метод (Method), адресу клієнта (Peer), статус відповіді (Status) та тривалість (Elapsed ms),
/// а також прокидає CorrelationId з метаданих gRPC у Serilog LogContext.
/// </summary>
public class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var correlationHeader = context.RequestHeaders.FirstOrDefault(h =>
            string.Equals(h.Key, CorrelationIdMiddleware.HeaderName, StringComparison.OrdinalIgnoreCase));

        var correlationId = correlationHeader?.Value ?? Guid.NewGuid().ToString();

        using (LogContext.PushProperty("CorrelationId", correlationId))
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
                    "gRPC {Method} from {Peer}: {Status} in {Elapsed} ms",
                    context.Method,
                    context.Peer,
                    ex.StatusCode,
                    sw.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(
                    ex,
                    "gRPC {Method} from {Peer}: {Status} in {Elapsed} ms",
                    context.Method,
                    context.Peer,
                    StatusCode.Internal,
                    sw.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
