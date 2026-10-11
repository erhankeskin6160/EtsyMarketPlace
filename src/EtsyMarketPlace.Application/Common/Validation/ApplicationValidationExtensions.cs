using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EtsyMarketPlace.Application.Common.Validation;

public static class ApplicationValidationExtensions
{
    public static IServiceCollection AddApplicationValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationValidationExtensions).Assembly);
        return services;
    }
}
