using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Auth;
using CustomerSupport.Application.Features.Auth.Commands.Login;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IIdentityUserService> _identity = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<IMessageFactory> _messages = new();
    private readonly LoginCommandHandler _handler;

    private static readonly ApplicationUser LinkedUser =
        ApplicationUser.Create("layla@example.com", "layla", "Layla", "Haddad");
    private static readonly ApplicationUser StaffUser =
        ApplicationUser.Create("agent@example.com", "agent", "Agent", "User");

    public LoginCommandHandlerTests()
    {
        StaffUser.LinkCustomer(Guid.NewGuid());

        _messages
            .Setup(m => m.Success(It.IsAny<AuthResponse>(), It.IsAny<string>()))
            .Returns((AuthResponse data, string code) => Response<AuthResponse>.Ok(data, code, "OK"));
        _messages
            .Setup(m => m.Fail<AuthResponse>(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Returns((string code, MessageType type) => Response<AuthResponse>.Fail(code, code, type));

        _refresh
            .Setup(r => r.CreateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshToken.Create(Guid.NewGuid(), "refresh-token", TimeSpan.FromDays(7)));

        _handler = new LoginCommandHandler(
            _identity.Object, _tokenService.Object, _refresh.Object, _messages.Object,
            new Mock<ILogger<LoginCommandHandler>>().Object);
    }

    private LoginCommand Command(string email) => new(email, "Password123", "::1", "test");

    [Fact]
    [Trait("AC", "402")]
    public async Task PJ3_LinkedUser_Token_CarriesCustomerIdClaim()
    {
        _identity.Setup(x => x.FindByEmailAsync("layla@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(LinkedUser);
        _identity.Setup(x => x.CheckPasswordAsync(LinkedUser, It.IsAny<string>())).ReturnsAsync(true);
        _identity.Setup(x => x.GetRolesAsync(LinkedUser)).ReturnsAsync(new[] { "User" });

        LinkedUser.LinkCustomer(Guid.NewGuid());

        IEnumerable<Claim>? captured = null;
        _tokenService
            .Setup(x => x.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Claim>>()))
            .Callback((Guid _, string _, IEnumerable<string> _, IEnumerable<Claim>? claims) => captured = claims)
            .Returns("access-token");

        await _handler.Handle(Command("layla@example.com"), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Select(c => c.Type).Should().Contain(AuthClaimTypes.CustomerId);
        captured.Single(c => c.Type == AuthClaimTypes.CustomerId).Value.Should().Be(LinkedUser.CustomerId!.Value.ToString());
    }

    [Fact]
    [Trait("AC", "403")]
    public async Task PJ4_StaffUser_Token_HasNoCustomerIdClaim()
    {
        var unlinked = ApplicationUser.Create("agent@example.com", "agent", "Agent", "User");
        _identity.Setup(x => x.FindByEmailAsync("agent@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(unlinked);
        _identity.Setup(x => x.CheckPasswordAsync(unlinked, It.IsAny<string>())).ReturnsAsync(true);
        _identity.Setup(x => x.GetRolesAsync(unlinked)).ReturnsAsync(new[] { "Agent" });

        IEnumerable<Claim>? captured = null;
        _tokenService
            .Setup(x => x.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Claim>>()))
            .Callback((Guid _, string _, IEnumerable<string> _, IEnumerable<Claim>? claims) => captured = claims)
            .Returns("access-token");

        await _handler.Handle(Command("agent@example.com"), CancellationToken.None);

        if (captured is not null)
        {
            captured.Select(c => c.Type).Should().NotContain(AuthClaimTypes.CustomerId);
        }
    }
}