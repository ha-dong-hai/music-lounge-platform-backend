// CoreFlow: All — first behavior in the pipeline; logs every request and its elapsed time.
// Runs for both commands and queries. Helps trace slow operations and diagnose failures
// without scattering log calls across every handler.
using MediatR;
using Microsoft.Extensions.Logging;

namespace MusicLounge.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {RequestName}", requestName);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        // Log as Warning when a single operation takes longer than 500ms — SLA signal
        if (stopwatch.ElapsedMilliseconds > 500)
            _logger.LogWarning("Slow request {RequestName} completed in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
        else
            _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);

        return response;
    }
}
