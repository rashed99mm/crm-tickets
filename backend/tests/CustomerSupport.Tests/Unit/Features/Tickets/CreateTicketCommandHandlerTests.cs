using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Tickets.Commands.CreateTicket;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Tickets;

public class CreateTicketCommandHandlerTests
{
    private readonly FakeTicketRepository _tickets = new();
    private readonly Mock<IRepository<Customer>> _customers = new();
    private readonly Mock<IRepository<Category>> _categories = new();
    private readonly Mock<IRepository<SLAPolicy>> _slaPolicies = new();
    private readonly Mock<IBusinessHoursCalculator> _calculator = new();
    private readonly Mock<ITicketReferenceGenerator> _references = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IIdentityUserService> _identityUsers = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly StubMessageFactory _messages = new();
    private readonly CreateTicketCommandHandler _handler;

    public CreateTicketCommandHandlerTests()
    {
        _customers.Setup(c => c.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Customer, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _categories.Setup(c => c.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _slaPolicies.Setup(s => s.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SLAPolicy, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<SLAPolicy>());
        _calculator.Setup(c => c.AddBusinessHours(It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((DateTime start, decimal _, Guid? _, CancellationToken _) => start);
        _references.Setup(r => r.NextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("TKT-1000000");
        _userContext.Setup(u => u.UserId).Returns(Guid.NewGuid());
        _identityUsers.Setup(i => i.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid _, CancellationToken _) => null);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _handler = new CreateTicketCommandHandler(
            _tickets, _customers.Object, _categories.Object, _slaPolicies.Object, _calculator.Object,
            _references.Object, _userContext.Object, _identityUsers.Object, _unitOfWork.Object, _messages);
    }

    private static CreateTicketCommand Command(string? source = null) =>
        new("Subject", "Description", Guid.NewGuid(), Guid.NewGuid(),
            Impact: source is null ? "Low" : null, Urgency: source is null ? "Low" : null, Source: source);

    [Fact]
    [Trait("AC", "404")]
    public async Task PJ5_PortalSource_IsStampedOnTheCreatedTicket()
    {
        var result = await _handler.Handle(Command(source: "Portal"), CancellationToken.None);

        result.Success.Should().BeTrue();
        _tickets.Added.Should().ContainSingle();
        _tickets.Added.Single().Source.Should().Be("Portal");
    }

    [Fact]
    [Trait("AC", "404")]
    public async Task PJ5_StaffSource_IsNull()
    {
        var result = await _handler.Handle(Command(source: null), CancellationToken.None);

        result.Success.Should().BeTrue();
        _tickets.Added.Single().Source.Should().BeNull();
    }

    private sealed class FakeTicketRepository : IRepository<Ticket>
    {
        public List<Ticket> Added { get; } = new();

        public Task AddAsync(Ticket entity, CancellationToken ct = default) { Added.Add(entity); return Task.CompletedTask; }
        public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Ticket?> GetTrackedAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Ticket?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<Ticket, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(System.Linq.Expressions.Expression<Func<Ticket, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Ticket>> ListAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Ticket>> ListAsync(System.Linq.Expressions.Expression<Func<Ticket, bool>>? predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Ticket>> ListOrderedAsync<TOrderKey>(System.Linq.Expressions.Expression<Func<Ticket, bool>>? predicate, System.Linq.Expressions.Expression<Func<Ticket, TOrderKey>> orderBy, bool descending, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TDto>> ListProjectedAsync<TDto>(System.Linq.Expressions.Expression<Func<Ticket, bool>>? predicate, System.Linq.Expressions.Expression<Func<Ticket, TDto>> selectExpression, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TDto>> ListProjectedOrderedAsync<TDto, TOrderKey>(System.Linq.Expressions.Expression<Func<Ticket, bool>>? predicate, System.Linq.Expressions.Expression<Func<Ticket, TDto>> selectExpression, System.Linq.Expressions.Expression<Func<Ticket, TOrderKey>> orderBy, bool descending, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, System.Linq.Expressions.Expression<Func<Ticket, bool>>? filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, System.Linq.Expressions.Expression<Func<Ticket, bool>>? filter, System.Linq.Expressions.Expression<Func<Ticket, TDto>> selectExpression, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Ticket> entities, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(Ticket entity) => throw new NotSupportedException();
        public void SetOriginalValue(Ticket entity, string propertyName, object? value) => throw new NotSupportedException();
        public void Remove(Ticket entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<Ticket> entities) => throw new NotSupportedException();
        public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<Ticket, bool>>? predicate = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class StubMessageFactory : IMessageFactory
    {
        public Response<T> Success<T>(T data, string domainKey) => Response<T>.Ok(data, domainKey, "OK");
        public Response<T> Fail<T>(string domainKey, MessageType type) => Response<T>.Fail(domainKey, domainKey, type);
        public Response<T> Fail<T>(string domainKey, MessageType type, IList<FieldError> errors) => Response<T>.Fail(domainKey, domainKey, type, errors);
        public Response<T> NotFound<T>(string domainKey) => Response<T>.Fail(domainKey, domainKey, MessageType.NotFound);
        public Response<T> Validation<T>(string domainKey, IList<FieldError> errors) => Response<T>.Fail(domainKey, domainKey, MessageType.Validation, errors);
    }
}