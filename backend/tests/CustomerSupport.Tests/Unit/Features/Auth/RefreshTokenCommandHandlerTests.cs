using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Auth;
using CustomerSupport.Application.Features.Auth.Commands.RefreshToken;
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

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IIdentityUserService> _identity = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<IMessageFactory> _messages = new();
    private readonly RefreshTokenCommandHandler _handler;

    private static readonly ApplicationUser LinkedUser =
        ApplicationUser.Create("layla@example.com", "layla", "Layla", "Haddad");
    private static readonly Guid UserId = Guid.NewGuid();

    public RefreshTokenCommandHandlerTests()
    {
        LinkedUser.LinkCustomer(Guid.NewGuid());

        _messages
            .Setup(m => m.Success(It.IsAny<AuthResponse>(), It.IsAny<string>()))
            .Returns((AuthResponse data, string code) => Response<AuthResponse>.Ok(data, code, "OK"));
        _messages
            .Setup(m => m.Fail<AuthResponse>(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Returns((string code, MessageType type) => Response<AuthResponse>.Fail(code, code, type));

        _tokenService.Setup(t => t.GetUserIdFromToken(It.IsAny<string>())).Returns(UserId);
        _refresh.Setup(r => r.ValidateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _refresh.Setup(r => r.RevokeRefreshTokenAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _refresh.Setup(r => r.CreateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshToken.Create(Guid.NewGuid(), "refresh-token", TimeSpan.FromDays(7)));

        _handler = new RefreshTokenCommandHandler(
            _identity.Object, _tokenService.Object, _refresh.Object, _messages.Object,
            new Mock<ILogger<RefreshTokenCommandHandler>>().Object);
    }

    [Fact]
    [Trait("AC", "402")]
    public async Task PJ3_Refresh_ReissuesCustomerIdClaimForLinkedUser()
    {
        _identity.Setup(x => x.FindByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(LinkedUser);
        _identity.Setup(x => x.GetRolesAsync(LinkedUser)).ReturnsAsync(new[] { "User" });

        IEnumerable<Claim>? captured = null;
        _tokenService
            .Setup(x => x.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Claim>>()))
            .Callback((Guid _, string _, IEnumerable<string> _, IEnumerable<Claim>? claims) => captured = claims)
            .Returns("access-token");

        await _handler.Handle(new RefreshTokenCommand("access", "refresh", "::1", "test"), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Single(c => c.Type == AuthClaimTypes.CustomerId).Value.Should().Be(LinkedUser.CustomerId!.Value.ToString());
    }

    [Fact]
    [Trait("AC", "403")]
    public async Task PJ4_Refresh_HasNoCustomerIdClaimForUnlinkedUser()
    {
        var unlinked = ApplicationUser.Create("agent@example.com", "agent", "Agent", "User");
        _identity.Setup(x => x.FindByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(unlinked);
        _identity.Setup(x => x.GetRolesAsync(unlinked)).ReturnsAsync(new[] { "Agent" });

        IEnumerable<Claim>? captured = null;
        _tokenService
            .Setup(x => x.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Claim>>()))
            .Callback((Guid _, string _, IEnumerable<string> _, IEnumerable<Claim>? claims) => captured = claims)
            .Returns("access-token");

        await _handler.Handle(new RefreshTokenCommand("access", "refresh", "::1", "test"), CancellationToken.None);

        if (captured is not null)
        {
            captured.Select(c => c.Type).Should().NotContain(AuthClaimTypes.CustomerId);
        }
    }
}
