using CustomerSupport.Application.Features.Auth.Commands.Login;
using CustomerSupport.Application.Features.Auth.Validators;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ValidLogin_ShouldPass()
    {
        var command = new LoginCommand("test@example.com", "Password123", null, null);
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    public async Task Validate_InvalidEmail_ShouldFail(string email)
    {
        var command = new LoginCommand(email, "Password123", null, null);
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_EmptyPassword_ShouldFail(string? password)
    {
        var command = new LoginCommand("test@example.com", password!, null, null);
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}
