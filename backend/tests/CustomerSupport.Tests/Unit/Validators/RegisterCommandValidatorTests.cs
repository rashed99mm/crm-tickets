using CustomerSupport.Application.Features.Auth.Commands.Register;
using CustomerSupport.Application.Features.Auth.Validators;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Validators;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ValidRegistration_ShouldPass()
    {
        var command = new RegisterCommand(
            "test@example.com",
            "testuser",
            "Password123",
            "John",
            "Doe",
            null,
            null,
            null
        );
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    public async Task Validate_InvalidEmail_ShouldFail(string email)
    {
        var command = new RegisterCommand(
            email,
            "testuser",
            "Password123",
            "John",
            "Doe",
            null,
            null,
            null
        );
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("user with spaces")]
    [InlineData("user@special!chars")]
    public async Task Validate_InvalidUsername_ShouldFail(string username)
    {
        var command = new RegisterCommand(
            "test@example.com",
            username,
            "Password123",
            "John",
            "Doe",
            null,
            null,
            null
        );
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("NoNumbers")]
    [InlineData("nouppercase1")]
    public async Task Validate_InvalidPassword_ShouldFail(string password)
    {
        var command = new RegisterCommand(
            "test@example.com",
            "testuser",
            password,
            "John",
            "Doe",
            null,
            null,
            null
        );
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Theory]
    [InlineData("")]
    public async Task Validate_EmptyFirstName_ShouldFail(string firstName)
    {
        var command = new RegisterCommand(
            "test@example.com",
            "testuser",
            "Password123",
            firstName,
            "Doe",
            null,
            null,
            null
        );
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    // ASG-8: phone is optional and bounded in length.
    [Fact]
    public async Task Validate_BlankPhone_ShouldPass()
    {
        var command = new RegisterCommand(
            "test@example.com",
            "testuser",
            "Password123",
            "John",
            "Doe",
            "   ",
            null,
            null
        );
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_OverLengthPhone_ShouldFail()
    {
        var command = new RegisterCommand(
            "test@example.com",
            "testuser",
            "Password123",
            "John",
            "Doe",
            new string('5', 21),
            null,
            null
        );
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PhoneNumber");
    }
}
