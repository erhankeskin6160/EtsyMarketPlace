using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Application.Common.Behaviours;

/// <summary>
/// Tüm MediatR komut ve sorgularının çalışma sürelerini ve durumlarını izleyen loglama davranışı.
/// </summary>
public class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("[MediatR İstek Başladı] {RequestName}", requestName);

        try
        {
            var response = await next();
            stopwatch.Stop();
            logger.LogInformation("[MediatR İstek Tamamlandı] {RequestName} ({ElapsedMilliseconds} ms)", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch
        {
            stopwatch.Stop();
            logger.LogError("[MediatR İstek Hata Aldı] {RequestName} ({ElapsedMilliseconds} ms)", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
