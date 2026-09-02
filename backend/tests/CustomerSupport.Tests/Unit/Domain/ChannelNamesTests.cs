using CustomerSupport.Domain.Common;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

public class ChannelNamesTests
{
    [Fact]
    public void CC48_AllContainsEverySupportedChannel()
    {
        ChannelNames.All.Should().BeEquivalentTo(
            ["Email", "System", "WhatsApp", "SMS", "WebForm", "LiveChat", "Portal"]);
    }

    [Fact]
    public void CC48_InboundIsASubsetOfAll_AndIncludesEmail()
    {
        ChannelNames.Inbound.Should().BeSubsetOf(ChannelNames.All);
        ChannelNames.Inbound.Should().Contain("Email");
    }

    [Fact]
    public void CC48_EveryNameFitsThePersistedColumn()
    {
        // TicketMessageConfiguration.cs:15 caps Channel at 20 characters.
        ChannelNames.All.Should().OnlyContain(name => name.Length <= 20);
    }
}
