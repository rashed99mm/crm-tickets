using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.PlatformSettings;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using CustomerSupport.Application.Features.PlatformSettings.Queries.GetBranding;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.PlatformSettings;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Admin;

/// <summary>
/// <c>GET /api/PlatformSettings/branding</c> is documented on its controller action as "Public —
/// used by both shells on load", and nothing seeds the three <c>brand.*</c> settings. So on a fresh
/// database the endpoint 404s on **every** admin page load, twice per load (the shell asks, then
/// the settings screen asks), and the settings screen renders "Setting not found" across its
/// Global Branding panel.
///
/// Unconfigured branding is not a missing resource — it means "use the defaults", which this
/// handler already computes and then discarded by failing.
/// </summary>
public sealed class GetBrandingQueryTests
{
    private static PlatformSetting Setting(string key, string value) =>
        new() { Key = key, Value = value, Category = "Branding", ValueType = "String" };

    private static (GetBrandingQueryHandler Handler, Mock<IMessageFactory> Messages) CreateSut(
        params PlatformSetting[] settings)
    {
        var repo = new Mock<IRepository<PlatformSetting>>();
        repo.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var messages = new Mock<IMessageFactory>();
        messages
            .Setup(m => m.Success(It.IsAny<BrandingDto>(), It.IsAny<string>()))
            .Returns((BrandingDto dto, string key) => Response<BrandingDto>.Ok(dto, key, key));
        messages
            .Setup(m => m.Fail<BrandingDto>(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Returns((string key, MessageType type) => Response<BrandingDto>.Fail(key, key, type));

        return (new GetBrandingQueryHandler(repo.Object, messages.Object), messages);
    }

    [Fact]
    public async Task NothingConfigured_ReturnsTheDefaults_NotNotFound()
    {
        var (handler, _) = CreateSut();

        var result = await handler.Handle(new GetBrandingQuery(), CancellationToken.None);

        result.Success.Should().BeTrue("unconfigured branding means defaults, not a missing resource");
        result.Data!.PrimaryColor.Should().Be("#2563EB");
        result.Data.AccentColor.Should().Be("#2563EB");
        result.Data.LogoUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfiguredValues_AreReturned()
    {
        var (handler, _) = CreateSut(
            Setting(BrandingKeys.LogoUrl, "https://cdn.example.com/logo.svg"),
            Setting(BrandingKeys.PrimaryColor, "#101828"),
            Setting(BrandingKeys.AccentColor, "#7C3AED"));

        var result = await handler.Handle(new GetBrandingQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.LogoUrl.Should().Be("https://cdn.example.com/logo.svg");
        result.Data.PrimaryColor.Should().Be("#101828");
        result.Data.AccentColor.Should().Be("#7C3AED");
    }

    [Fact]
    public async Task PartiallyConfigured_FillsOnlyTheGaps()
    {
        // The half-configured case was always handled correctly — the bug was only the all-empty
        // one — so this pins the behaviour that must not regress while fixing it.
        var (handler, _) = CreateSut(Setting(BrandingKeys.PrimaryColor, "#101828"));

        var result = await handler.Handle(new GetBrandingQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.PrimaryColor.Should().Be("#101828");
        result.Data.AccentColor.Should().Be("#2563EB");
    }
}
