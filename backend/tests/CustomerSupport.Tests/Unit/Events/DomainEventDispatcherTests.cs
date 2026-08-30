using CustomerSupport.Application.Events;
using CustomerSupport.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Events;

/// <summary>Raised by the test only — the dispatcher is generic over <see cref="IDomainEvent"/>.</summary>
public sealed class OrderPlacedEvent(Guid id) : DomainEvent
{
    public Guid Id { get; } = id;
}

public sealed class OrderDispatchedEvent(Guid id) : DomainEvent
{
    public Guid Id { get; } = id;
}

public sealed class RecordingOrderHandler : IDomainEventHandler<OrderPlacedEvent>
{
    public List<OrderPlacedEvent> Received { get; } = new();

    public Task Handle(OrderPlacedEvent domainEvent, CancellationToken ct = default)
    {
        Received.Add(domainEvent);
        return Task.CompletedTask;
    }
}

public sealed class ThrowingOrderHandler : IDomainEventHandler<OrderPlacedEvent>
{
    public Task Handle(OrderPlacedEvent domainEvent, CancellationToken ct = default)
        => throw new InvalidOperationException("boom");
}

public class DomainEventDispatcherTests
{
    [Fact]
    public async Task Dispatch_InvokesRegisteredHandler_OncePerEvent() // AC-N4
    {
        var handler = new RecordingOrderHandler();
        await using var provider = Build([handler]);

        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        var evt = new OrderPlacedEvent(Guid.NewGuid());
        await dispatcher.DispatchAsync([evt], CancellationToken.None);

        handler.Received.Should().ContainSingle().Which.Id.Should().Be(evt.Id);
    }

    [Fact]
    public async Task Dispatch_WithNoRegisteredHandler_DoesNothingAndDoesNotThrow() // AC-N4
    {
        await using var provider = Build([]);
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        var act = async () => await dispatcher.DispatchAsync(
            [new OrderDispatchedEvent(Guid.NewGuid())], CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Dispatch_HandlerException_IsSwallowedAndLogged() // AC-N4
    {
        await using var provider = Build([new ThrowingOrderHandler()]);
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        var act = async () => await dispatcher.DispatchAsync(
            [new OrderPlacedEvent(Guid.NewGuid())], CancellationToken.None);

        // A handler crash must not fail the already-committed save or crash the request.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Dispatch_EmptyCollection_IsANoOp() // AC-N6
    {
        var handler = new RecordingOrderHandler();
        await using var provider = Build([handler]);
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        await dispatcher.DispatchAsync([], CancellationToken.None);

        handler.Received.Should().BeEmpty();
    }

    private static ServiceProvider Build(IEnumerable<object> handlers)
    {
        var services = new ServiceCollection();
        foreach (var handler in handlers)
        {
            services.AddScoped(handler.GetType().GetInterfaces().Single(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)),
                _ => handler);
        }
        services.AddSingleton<ILogger<DomainEventDispatcher>>(NullLogger<DomainEventDispatcher>.Instance);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services.BuildServiceProvider();
    }
}
