using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace EtsyMarketPlace.Application.Common.Behaviours;

/// <summary>
/// MediatR istekleri öncesinde FluentValidation kurallarını çalıştıran ve hata durumunda ValidationErrorResponse dönen pipeline davranışı.
/// </summary>
public class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

        if (failures.Count != 0)
        {
            var errors = failures
                .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());

            if (typeof(ServiceResponse).IsAssignableFrom(typeof(TResponse)))
            {
                var validationResponse = new ValidationErrorResponse(errors);
                return (TResponse)(object)validationResponse;
            }

            throw new ValidationException(failures);
        }

        return await next();
    }
}
