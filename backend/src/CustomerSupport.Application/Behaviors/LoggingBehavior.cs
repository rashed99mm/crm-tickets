using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CustomerSupport.Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next();
        sw.Stop();
        logger.LogInformation("Handled {RequestType} in {ElapsedMs}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        return response;
    }
}
