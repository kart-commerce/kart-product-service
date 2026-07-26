using System.Reflection;
using FluentValidation;
using Kart.Product.Application.Common.Behaviours;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Product.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Registration order = pipeline order (outermost first): Logging wraps Validation.
            cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
