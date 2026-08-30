using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;

using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Auth.Commands.Register;

/// <summary>
/// Registers a new user account with the default role.
/// </summary>
public class RegisterCommandHandler : ICommandHandler<RegisterCommand, Response<Guid>>
{
    private readonly IIdentityUserService _identityUserService;
    private readonly IRepository<Customer> _customers;
    private readonly IDbExceptionTranslator _dbExceptionTranslator;
    private readonly IMessageFactory _messages;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IIdentityUserService identityUserService,
        IRepository<Customer> customers,
        IDbExceptionTranslator dbExceptionTranslator,
        IMessageFactory messages,
        ILogger<RegisterCommandHandler> logger)
    {
        _identityUserService = identityUserService;
        _customers = customers;
        _dbExceptionTranslator = dbExceptionTranslator;
        _messages = messages;
        _logger = logger;
    }

    /// <summary>
    /// Handles the user registration command.
    /// </summary>
    /// <param name="request">Registration details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the created user identifier or a localized error.</returns>
    public async Task<Response<Guid>> Handle(RegisterCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Processing user registration");

        var existingUser = await _identityUserService.FindByEmailAsync(request.Email, ct);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration failed — email already exists");
            return _messages.Fail<Guid>(ApplicationErrors.User.EMAIL_EXISTS, MessageType.Conflict);
        }

        var existingUsername = await _identityUserService.FindByUsernameAsync(request.Username, ct);
        if (existingUsername != null)
        {
            _logger.LogWarning("Registration failed — username already exists");
            return _messages.Fail<Guid>(ApplicationErrors.User.USERNAME_EXISTS, MessageType.Conflict);
        }

        var user = ApplicationUser.Create(
            request.Email,
            request.Username,
            request.FirstName,
            request.LastName);

        user.PhoneNumber = NormalizePhone(request.PhoneNumber);

        if (request.IsPortalRegistration)
        {
            // US-401 / PJ-2. The customer row is added to the same scoped DbContext the identity
            // store writes through, so its single SaveChanges inside CreateAsync flushes both rows
            // atomically — a failure saves neither, and an email already taken as a customer (but
            // never registered as a user) surfaces as EMAIL_EXISTS, not a 500.
            Customer customer;
            try
            {
                customer = Customer.Create(
                    $"{request.FirstName} {request.LastName}".Trim(),
                    request.Email,
                    request.PhoneNumber);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Portal registration failed — {Reason}", ex.Message);
                return _messages.Fail<Guid>(ApplicationErrors.Validation.INVALID_EMAIL, MessageType.Validation);
            }

            await _customers.AddAsync(customer, ct);
            user.LinkCustomer(customer.Id);
        }

        try
        {
            var result = await _identityUserService.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors);
                _logger.LogError("User creation failed — identity errors: {Errors}", errors);
                return _messages.Fail<Guid>(ApplicationErrors.General.INTERNAL_ERROR, MessageType.Internal);
            }
        }
        catch (Exception ex) when (request.IsPortalRegistration && _dbExceptionTranslator.IsUniqueViolation(ex))
        {
            // PJ-2. The email was free as a user but is taken as a customer. The single SaveChanges
            // inside CreateAsync rejected the whole unit of work, so the orphaned customer row is
            // gone too — the account was not created, and the conflict carries the customer shape.
            _logger.LogWarning("Portal registration failed — customer email already exists");
            return _messages.Fail<Guid>(ApplicationErrors.Customer.EMAIL_EXISTS, MessageType.Conflict);
        }

        var defaultRole = ApplicationRole.Roles.User;
        await _identityUserService.EnsureRoleExistsAsync(defaultRole, "Regular user role", ct);
        await _identityUserService.AddToRoleAsync(user, defaultRole);

        _logger.LogInformation("User {UserId} registered successfully", user.Id);

        return _messages.Success(user.Id, ApplicationErrors.General.SUCCESS_CREATED);
    }

    private static string? NormalizePhone(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        return phoneNumber.Trim();
    }
}
