using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Application.Common.Behaviours;

/// <summary>
/// İşlenmeyen istisnaları yakalayıp standart ServiceResponse hata formatına çeviren pipeline davranışı.
/// </summary>
public class ExceptionBehaviour<TRequest, TResponse>(ILogger<ExceptionBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;
            logger.LogError(ex, "[MediatR Yakalanan Hata] {RequestName} çalışırken hata oluştu: {Message}", requestName, ex.Message);

            if (typeof(ServiceResponse).IsAssignableFrom(typeof(TResponse)))
            {
                var errorResponse = ServiceResponse.CreateError($"Sunucu hatası: {ex.Message}", 500);
                return (TResponse)(object)errorResponse;
            }

            throw;
        }
    }
}
