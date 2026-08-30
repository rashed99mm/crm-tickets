using CustomerSupport.Domain.Entities.Tickets;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit;

public class TicketMessageTests
{
    private static readonly Guid TicketId = Guid.NewGuid();
    private static readonly Guid SenderId = Guid.NewGuid();

    [Fact]
    public void Create_ValidFields_StoresThemAndStampsSentAt()
    {
        var before = DateTime.UtcNow;

        var message = TicketMessage.Create(TicketId, "Outbound", "System", "Follow-up", "Called back.", SenderId);

        message.TicketId.Should().Be(TicketId);
        message.Direction.Should().Be("Outbound");
        message.Channel.Should().Be("System");
        message.Subject.Should().Be("Follow-up");
        message.Body.Should().Be("Called back.");
        message.SenderId.Should().Be(SenderId);
        message.SentAt.Should().BeOnOrAfter(before);
        message.Id.Should().Be(Guid.Empty); // unassigned — EF generates it, same reasoning as TicketHistory.Record
    }

    [Fact]
    public void Create_NoSubject_IsAllowed()
    {
        var message = TicketMessage.Create(TicketId, "Inbound", "Email", null, "Customer called.", SenderId);

        message.Subject.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyBody_Throws(string body)
    {
        var act = () => TicketMessage.Create(TicketId, "Outbound", "System", null, body, SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("body");
    }

    [Fact]
    public void Create_BodyOverMaxLength_Throws()
    {
        var act = () => TicketMessage.Create(TicketId, "Outbound", "System", null, new string('a', 4001), SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("body");
    }

    [Fact]
    public void Create_SubjectOverMaxLength_Throws()
    {
        var act = () => TicketMessage.Create(TicketId, "Outbound", "System", new string('a', 201), "Body", SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("subject");
    }

    [Theory]
    [InlineData("Sideways")]
    [InlineData("")]
    public void Create_UnrecognisedDirection_Throws(string direction)
    {
        var act = () => TicketMessage.Create(TicketId, direction, "System", null, "Body", SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("direction");
    }

    [Theory]
    [InlineData("Carrier Pigeon")]
    [InlineData("")]
    public void Create_UnrecognisedChannel_Throws(string channel)
    {
        var act = () => TicketMessage.Create(TicketId, "Outbound", channel, null, "Body", SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("channel");
    }

    // --- FEAT-24..27 — external channels (CC-1) and provider ids (CC-9/CC-12) ---------------------

    [Theory]
    [InlineData("Email")]
    [InlineData("System")]
    [InlineData("WhatsApp")]
    [InlineData("SMS")]
    [InlineData("WebForm")]
    [InlineData("LiveChat")]
    public void Create_AnyAllowedChannel_IsAccepted(string channel)
    {
        var message = TicketMessage.Create(TicketId, "Inbound", channel, null, "Hello", SenderId);

        message.Channel.Should().Be(channel);
    }

    [Fact]
    public void Create_ProviderMessageId_IsStored()
    {
        var message = TicketMessage.Create(TicketId, "Inbound", "WhatsApp", null, "Hello", SenderId, "wamid.ABC123");

        message.ProviderMessageId.Should().Be("wamid.ABC123");
    }

    [Fact]
    public void Create_NoProviderMessageId_DefaultsToNull()
    {
        var message = TicketMessage.Create(TicketId, "Outbound", "System", null, "Hello", SenderId);

        message.ProviderMessageId.Should().BeNull();
    }

    [Fact]
    public void Create_EmptySenderId_Throws()
    {
        var act = () => TicketMessage.Create(TicketId, "Outbound", "System", null, "Body", Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("senderId");
    }

    [Fact]
    public void Create_EmptyTicketId_Throws()
    {
        var act = () => TicketMessage.Create(Guid.Empty, "Outbound", "System", null, "Body", SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("ticketId");
    }
}
