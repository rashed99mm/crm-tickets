using CustomerSupport.Application.Behaviors;
using CustomerSupport.Application.Events;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CustomerSupport.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterPlatformApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ResponseValidationBehavior<,>));
            // Registered last (innermost, closest to the handler) deliberately: a validation
            // failure short-circuits before calling next(), so this never runs for a request that
            // never reached the handler (AC-145) without needing its own success check for that
            // case — only for a handler-level failure, which it still has to check explicitly.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        });
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Domain-event publishing: the dispatcher resolves handlers by a fresh scope and the
        // closed event interface; the AppDbContext raises after every committed save. Every
        // IDomainEventHandler<TEvent> in this assembly (e.g. the ticket-created notification
        // handler) is registered here automatically.
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        foreach (var handlerType in Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                && t.GetInterfaces().Any(i => i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))))
        {
            foreach (var contract in handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)))
            {
                services.AddScoped(contract, handlerType);
            }
        }

        // Multi-turn chatbot (AI-38..AI-40) — the shared engine behind staff and portal chat routes.
        services.AddScoped<CustomerSupport.Application.Features.Ai.Chat.AiChatService>();

        return services;
    }
}
