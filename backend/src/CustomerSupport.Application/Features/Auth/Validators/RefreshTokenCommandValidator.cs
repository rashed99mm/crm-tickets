using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Auth.Commands.RefreshToken;
using FluentValidation;

namespace CustomerSupport.Application.Features.Auth.Validators;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("Access token is required").WithErrorCode(ApplicationErrors.Validation.TOKEN_REQUIRED);

        RuleFor(x => x.RefreshTokenValue)
            .NotEmpty().WithMessage("Refresh token is required").WithErrorCode(ApplicationErrors.Validation.TOKEN_REQUIRED);
    }
}
