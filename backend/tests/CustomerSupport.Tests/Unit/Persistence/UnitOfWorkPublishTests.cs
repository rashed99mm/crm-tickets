using CustomerSupport.Application.Events;
using CustomerSupport.Domain.Events;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Domain.Entities.Customers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Persistence;

public sealed class UowCreatedEvent(Guid customerId) : DomainEvent
{
    public Guid CustomerId { get; } = customerId;
}

public sealed class RecordingUowHandler : IDomainEventHandler<UowCreatedEvent>
{
    public List<UowCreatedEvent> Received { get; } = new();

    public Task Handle(UowCreatedEvent domainEvent, CancellationToken ct = default)
    {
        Received.Add(domainEvent);
        return Task.CompletedTask;
    }
}

public class UnitOfWorkPublishTests
{
    private sealed record Harness(AppDbContext Context, UnitOfWork UnitOfWork, RecordingUowHandler Handler);

    private static Harness Build()
    {
        var handler = new RecordingUowHandler();
        var services = new ServiceCollection();
        services.AddScoped(typeof(IDomainEventHandler<UowCreatedEvent>), _ => handler);
        services.AddSingleton<ILogger<DomainEventDispatcher>>(NullLogger<DomainEventDispatcher>.Instance);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        var provider = services.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);

        var uow = new UnitOfWork(context, provider.GetRequiredService<IServiceScopeFactory>());
        return new Harness(context, uow, handler);
    }

    [Fact]
    public async Task SaveChanges_PublishesRaisedEvent_AfterCommit_AtMostOnce() // AC-N4
    {
        var h = Build();
        var customer = Customer.Create("Nadia", $"uow-{Guid.NewGuid():N}@test.local", null);
        customer.AddDomainEvent(new UowCreatedEvent(customer.Id));
        h.Context.Customers.Add(customer);

        await h.UnitOfWork.SaveChangesAsync(CancellationToken.None);

        var published = h.Handler.Received.Should().ContainSingle().Subject;
        published.CustomerId.Should().Be(customer.Id);

        // The same unit of work saved again with nothing new raised must not re-publish (cleared).
        await h.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        h.Handler.Received.Should().ContainSingle();
    }

    [Fact]
    public async Task SaveChanges_WithNoRaisedEvent_DispatchesNothing() // AC-N6
    {
        var h = Build();
        var customer = Customer.Create("Nadia", $"uow2-{Guid.NewGuid():N}@test.local", null);
        h.Context.Customers.Add(customer);

        await h.UnitOfWork.SaveChangesAsync(CancellationToken.None);

        h.Handler.Received.Should().BeEmpty();
    }
}
