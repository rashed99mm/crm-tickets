using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>
/// The closed set of notification channels. FEAT-24 added <c>WhatsApp</c>; the point of this test
/// is that every channel a ticket message or a notification dispatch can name resolves through one
/// factory, so a new channel (and its <c>Is*</c> helper) cannot ship half-wired.
/// </summary>
public class NotificationChannelTests
{
    [Theory]
    [InlineData("InApp")]
    [InlineData("Email")]
    [InlineData("SMS")]
    [InlineData("Push")]
    [InlineData("WhatsApp")]
    public void Create_KnownName_ReturnsTheSingleton(string name)
    {
        var channel = NotificationChannel.Create(name);

        channel.Value.Should().Be(name);
    }

    [Fact]
    public void Create_WhatsApp_IsTheWhatsAppSingleton()
    {
        NotificationChannel.Create("WhatsApp").Should().Be(NotificationChannel.WhatsApp);
        NotificationChannel.WhatsApp.IsWhatsApp.Should().BeTrue();
        NotificationChannel.Email.IsWhatsApp.Should().BeFalse();
    }

    [Theory]
    [InlineData("Carrier Pigeon")]
    [InlineData("")]
    public void Create_UnknownName_Throws(string name)
    {
        var act = () => NotificationChannel.Create(name);

        act.Should().Throw<ArgumentException>().WithParameterName("channel");
    }
}