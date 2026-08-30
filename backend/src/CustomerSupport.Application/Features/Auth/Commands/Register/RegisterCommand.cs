using CustomerSupport.Application.Contracts;

using CustomerSupport.Application.Features.Auth.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Auth.Commands.Register;

public record RegisterCommand(
    string Email,
    string Username,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? IpAddress,
    string? UserAgent,

    /// <summary>Set only by the customer-facing host (US-401, PJ-2). When true the handler also
    /// creates the linked <c>Customer</c> record so the account owns a customer profile.</summary>
    bool IsPortalRegistration = false
) : ICommand<Response<Guid>>;
