using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Tickets.Commands.CreateTicket;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Validators;

public class CreateTicketCommandValidatorTests
{
    private readonly CreateTicketCommandValidator _validator = new();

    private static CreateTicketCommand Valid(string? source = null) =>
        new("Subject", "Description", Guid.NewGuid(), Guid.NewGuid(), "Low", source);

    [Fact]
    public async Task Validate_ValidCommand_ShouldPass()
    {
        var result = await _validator.ValidateAsync(Valid());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Portal")]
    [InlineData("WebForm")]
    [InlineData("WhatsApp")]
    public async Task Validate_KnownSource_ShouldPass(string source)
    {
        var result = await _validator.ValidateAsync(Valid(source));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("SmokeSignal")]
    [InlineData("portal")]
    public async Task Validate_InvalidSource_ShouldFail(string source)
    {
        var result = await _validator.ValidateAsync(Valid(source));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == ApplicationErrors.Validation.TICKET_SOURCE_INVALID);
    }
}