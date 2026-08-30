using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Auth.Commands.Register;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Auth;

/// <summary>
/// The registration handler (US-401, PJ-1/2). The portal flag must persist a linked <c>Customer</c>
/// row and a duplicate customer email must surface as a conflict, while a staff-host registration
/// (flag false) must not touch the customer store at all.
/// </summary>
public class RegisterCommandHandlerTests
{
    private readonly Mock<IIdentityUserService> _identity = new();
    private readonly Mock<IDbExceptionTranslator> _translator = new();
    private readonly Mock<IMessageFactory> _messages = new();
    private readonly Mock<ILogger<RegisterCommandHandler>> _logger = new();
    private readonly InMemoryCustomerRepository _customers = new();
    private readonly RegisterCommandHandler _handler;

    private static RegisterCommand Command(bool isPortal = false) =>
        new("Layla@Example.com", "layla", "Password123", "Layla", "Haddad", null, "::1", "test", isPortal);

    public RegisterCommandHandlerTests()
    {
        _identity
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationUser?)null);
        _identity
            .Setup(x => x.FindByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationUser?)null);
        _identity
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityOperationResult.Success());
        _identity
            .Setup(x => x.EnsureRoleExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _identity
            .Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityOperationResult.Success());

        _messages
            .Setup(m => m.Success(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns((Guid val, string code) => Response<Guid>.Ok(val, code, "OK"));
        _messages
            .Setup(m => m.Fail<Guid>(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Returns((string code, MessageType type) => Response<Guid>.Fail(code, code, type));

        _handler = new RegisterCommandHandler(
            _identity.Object,
            _customers,
            _translator.Object,
            _messages.Object,
            _logger.Object);
    }

    [Fact]
    [Trait("AC", "401")]
    public async Task PJ2_PortalRegistration_CreatesAndLinksCustomer()
    {
        var result = await _handler.Handle(Command(isPortal: true), CancellationToken.None);

        result.Success.Should().BeTrue();
        _customers.All.Should().ContainSingle();
        var customer = _customers.All.Single();
        customer.Name.Should().Be("Layla Haddad");
        customer.Email.Should().Be("layla@example.com");

        _identity.Verify(x => x.CreateAsync(
            It.Is<ApplicationUser>(u => u.CustomerId.HasValue && u.CustomerId.Value == customer.Id), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    [Trait("AC", "401")]
    public async Task PJ2_NonPortalRegistration_DoesNotTouchCustomerStore()
    {
        var result = await _handler.Handle(Command(isPortal: false), CancellationToken.None);

        result.Success.Should().BeTrue();
        _customers.All.Should().BeEmpty();
        _identity.Verify(x => x.CreateAsync(
            It.Is<ApplicationUser>(u => !u.CustomerId.HasValue), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    [Trait("AC", "401")]
    public async Task PJ2_PortalRegistration_DuplicateCustomerEmail_ReturnsConflict()
    {
        _identity
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("duplicate key"));
        _translator.Setup(t => t.IsUniqueViolation(It.IsAny<Exception>())).Returns(true);

        var result = await _handler.Handle(Command(isPortal: true), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Customer.EMAIL_EXISTS);
    }

    private sealed class InMemoryCustomerRepository : IRepository<Customer>
    {
        public List<Customer> All { get; } = new();

        public Task AddAsync(Customer entity, CancellationToken ct = default)
        {
            All.Add(entity);
            return Task.CompletedTask;
        }

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Customer?> GetTrackedAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Customer?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<Customer, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(System.Linq.Expressions.Expression<Func<Customer, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Customer>> ListAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Customer>> ListAsync(System.Linq.Expressions.Expression<Func<Customer, bool>>? predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Customer>> ListOrderedAsync<TOrderKey>(System.Linq.Expressions.Expression<Func<Customer, bool>>? predicate, System.Linq.Expressions.Expression<Func<Customer, TOrderKey>> orderBy, bool descending, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TDto>> ListProjectedAsync<TDto>(System.Linq.Expressions.Expression<Func<Customer, bool>>? predicate, System.Linq.Expressions.Expression<Func<Customer, TDto>> selectExpression, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TDto>> ListProjectedOrderedAsync<TDto, TOrderKey>(System.Linq.Expressions.Expression<Func<Customer, bool>>? predicate, System.Linq.Expressions.Expression<Func<Customer, TDto>> selectExpression, System.Linq.Expressions.Expression<Func<Customer, TOrderKey>> orderBy, bool descending, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, System.Linq.Expressions.Expression<Func<Customer, bool>>? filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, System.Linq.Expressions.Expression<Func<Customer, bool>>? filter, System.Linq.Expressions.Expression<Func<Customer, TDto>> selectExpression, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Customer> entities, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(Customer entity) => throw new NotSupportedException();
        public void SetOriginalValue(Customer entity, string propertyName, object? value) => throw new NotSupportedException();
        public void Remove(Customer entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<Customer> entities) => throw new NotSupportedException();
        public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<Customer, bool>>? predicate = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}