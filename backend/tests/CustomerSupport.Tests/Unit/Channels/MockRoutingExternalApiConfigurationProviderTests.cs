using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Infrastructure.ExternalApis.Providers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Channels;

public class MockRoutingExternalApiConfigurationProviderTests
{
    private readonly Mock<IExternalApiConfigurationProvider> _inner = new();

    private static ChannelOptions Options(bool useMocks) => new()
    {
        UseMocks = useMocks,
        MockBaseUrl = "http://localhost:3001",
        MockWebhookSecret = "dev-secret",
    };

    [Fact]
    public void CC30_FlagOff_DelegatesEverythingToTheDatabaseProvider()
    {
        var fromDb = new ExternalApiConfig { BaseUrl = "https://real.example/send" };
        _inner.Setup(p => p.GetConfig(NotificationGatewayConstants.EmailGatewayConfigName)).Returns(fromDb);

        var sut = new MockRoutingExternalApiConfigurationProvider(_inner.Object, Options(useMocks: false));

        sut.GetConfig(NotificationGatewayConstants.EmailGatewayConfigName).Should().BeSameAs(fromDb);
    }

    [Theory]
    [InlineData("EmailGateway", "http://localhost:3001/mock/sendgrid/v3/mail/send")]
    [InlineData("WhatsAppGateway", "http://localhost:3001/mock/meta/v18.0/100000000000000/messages")]
    public void CC31_FlagOn_RoutesTheChannelGatewaysToTheMock(string configName, string expectedUrl)
    {
        var sut = new MockRoutingExternalApiConfigurationProvider(_inner.Object, Options(useMocks: true));

        var config = sut.GetConfig(configName);

        config.Should().NotBeNull();
        config!.BaseUrl.Should().Be(expectedUrl);
        _inner.Verify(p => p.GetConfig(configName), Times.Never);
    }

    [Fact]
    public void CC31_FlagOn_LeavesEveryOtherConfigurationAlone()
    {
        var payments = new ExternalApiConfig { BaseUrl = "https://payments.example" };
        _inner.Setup(p => p.GetConfig("PaymentGateway")).Returns(payments);

        var sut = new MockRoutingExternalApiConfigurationProvider(_inner.Object, Options(useMocks: true));

        sut.GetConfig("PaymentGateway").Should().BeSameAs(payments);
    }

    [Fact]
    public void CC33_FlagOn_WorksWithNoDatabaseRowAtAll()
    {
        _inner.Setup(p => p.GetConfig(It.IsAny<string>())).Returns((ExternalApiConfig?)null);

        var sut = new MockRoutingExternalApiConfigurationProvider(_inner.Object, Options(useMocks: true));

        var config = sut.GetConfig(NotificationGatewayConstants.SmsGatewayConfigName);

        config.Should().NotBeNull();
        // Auth.Type None keeps ApplyAuth out of the way; the secret is still carried for the
        // inbound verifier, which reads Auth.Value regardless of Type.
        config!.Auth.Type.Should().Be(ExternalApiAuthType.None);
        config.Auth.Value.Should().Be("dev-secret");
    }

    [Theory]
    [InlineData(true, "Production", false)]
    [InlineData(true, "Development", true)]
    [InlineData(false, "Production", true)]
    [InlineData(true, null, true)]
    public void CC32_MocksAreIllegalInProductionOnly(bool useMocks, string? environment, bool expectedLegal)
    {
        ChannelMockGuard.Validate(useMocks, environment).IsLegal.Should().Be(expectedLegal);
    }
}
