using CustomerSupport.Application.Features.Tickets.Events;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Events.Tickets;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Tickets.Events;

public class TicketCreatedEventHandlerTests
{
    private readonly Mock<IIdentityUserService> _users = new();
    private readonly Mock<INotificationGateway> _gateway = new();
    private readonly TicketCreatedEventHandler _handler;

    public TicketCreatedEventHandlerTests()
    {
        _handler = new TicketCreatedEventHandler(
            _users.Object, _gateway.Object, NullLogger<TicketCreatedEventHandler>.Instance);
    }

    private static ApplicationUser User() =>
        ApplicationUser.Create($"customer-{Guid.NewGuid():N}@test.local", $"customer-{Guid.NewGuid():N}@test.local", "Portal", "User");

    private static TicketCreatedEvent Event(Guid actorId) =>
        new(Guid.NewGuid(), "TKT-1000042", Guid.NewGuid(), actorId);

    private List<NotificationDispatchRequest> Sent() =>
        _gateway.Invocations
            .Select(i => (NotificationDispatchRequest)i.Arguments[0])
            .ToList();

    private NotificationDispatchRequest RequireSingle(Guid recipient)
    {
        var matches = Sent().Where(s => s.RecipientUserId == recipient).ToList();
        matches.Should().ContainSingle();
        return matches.Single();
    }

    [Fact]
    public async Task Handle_LinkedCustomer_DispatchesToCustomerAndCreator() // AC-N1, AC-N7
    {
        var customerUser = User();
        _users.Setup(u => u.FindByCustomerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerUser);
        var @event = Event(actorId: Guid.NewGuid());

        await _handler.Handle(@event, CancellationToken.None);

        // Exactly two recipients, never the same one twice.
        _gateway.Invocations.Should().HaveCount(2);
        Sent().Select(s => s.RecipientUserId).Should().OnlyHaveUniqueItems();

        // AC-N1 customer leg.
        var customer = RequireSingle(customerUser.Id);
        customer.TemplateCode.Should().Be("TICKET_CREATED");
        customer.Channels.Should().ContainSingle().Which.Should().Be(NotificationChannel.InApp);
        customer.CorrelationId.Should().Be(@event.TicketId.ToString());
        customer.DeduplicationKey.Should().Be($"ticket-created:{@event.TicketId}:customer");
        customer.Variables.Should().ContainKey("Message").WhoseValue.Should().Contain(@event.Reference);
        customer.Variables.Should().ContainKey("Title").WhoseValue.Should().Be("Ticket created");

        // AC-N7 creator leg targets the actor with its own dedup key.
        var creator = RequireSingle(@event.ActorId);
        creator.TemplateCode.Should().Be("TICKET_CREATED");
        creator.Channels.Should().ContainSingle().Which.Should().Be(NotificationChannel.InApp);
        creator.DeduplicationKey.Should().Be($"ticket-created:{@event.TicketId}:creator");
        creator.CorrelationId.Should().Be(@event.TicketId.ToString());
        creator.Variables.Should().ContainKey("Message").WhoseValue.Should().Contain(@event.Reference);
    }

    [Fact]
    public async Task Handle_NoLinkedUser_StillNotifiesCreator() // AC-N5 + AC-N7
    {
        _users.Setup(u => u.FindByCustomerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationUser?)null);
        var @event = Event(actorId: Guid.NewGuid());

        var act = async () => await _handler.Handle(@event, CancellationToken.None);

        await act.Should().NotThrowAsync();
        var sent = RequireSingle(@event.ActorId);
        sent.TemplateCode.Should().Be("TICKET_CREATED");
    }

    [Fact]
    public async Task Handle_EmptyCustomerId_SkipsCustomerButStillNotifiesCreator() // AC-N5 + AC-N7
    {
        _users.Setup(u => u.FindByCustomerIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationUser?)null);
        var @event = new TicketCreatedEvent(Guid.NewGuid(), "TKT-1000043", Guid.Empty, actorId: Guid.NewGuid());

        var act = async () => await _handler.Handle(@event, CancellationToken.None);

        await act.Should().NotThrowAsync();
        RequireSingle(@event.ActorId);
        _gateway.Invocations.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_SelfServicePortalSubmission_DoesNotDuplicateTheSingleUser() // dedup
    {
        // A portal customer submitting their own ticket: the creator (ActorId) IS the linked user.
        var customerUser = User();
        _users.Setup(u => u.FindByCustomerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerUser);
        var @event = Event(actorId: customerUser.Id);

        await _handler.Handle(@event, CancellationToken.None);

        _gateway.Invocations.Should().HaveCount(1);
        Sent().Single().RecipientUserId.Should().Be(customerUser.Id);
    }
}
