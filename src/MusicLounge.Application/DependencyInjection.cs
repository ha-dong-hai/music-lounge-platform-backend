using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Behaviors;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Services;

namespace MusicLounge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        // MediatR executes pipeline behaviors in registration order (outermost first). Validation
        // runs before ActiveUser so a structurally-invalid request fails on cheap FluentValidation
        // checks alone, instead of paying a DB roundtrip (ActiveUserBehavior's IsActive re-check,
        // on every single authenticated request) before ever inspecting whether the input is even
        // well-formed.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ActiveUserBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
