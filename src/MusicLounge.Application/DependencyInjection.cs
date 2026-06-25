// CoreFlow: All — wires up all Application layer services into the DI container.
// Called once from the API layer. Keeps each layer responsible for its own registration.
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Behaviors;
using System.Reflection;

namespace MusicLounge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register all IRequestHandler implementations found in this assembly
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // Register all AbstractValidator<T> implementations found in this assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Pipeline executes in registration order: Logging → Validation → Transaction → Handler
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
