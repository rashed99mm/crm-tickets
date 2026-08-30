using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Portal.Dtos;
using CustomerSupport.Application.Features.Portal.Queries.GetPortalTickets;
using CustomerSupport.Application.Features.Portal.Queries.GetPortalTicketDetail;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Survey;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Portal;

public class PortalQueryTests
{
    private static Ticket OwnedTicket(Guid customerId, string reference) =>
        Ticket.Create(reference, "Subject", "Description", customerId, Guid.NewGuid(), "Low", Guid.NewGuid());

    [Fact]
    [Trait("AC", "405")]
    public async Task PJ8_List_ReturnsOnlyTheCallingCustomerTickets()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var repo = new FakeTicketRepository(new[]
        {
            OwnedTicket(owner, "TKT-1"),
            OwnedTicket(owner, "TKT-2"),
            OwnedTicket(other, "TKT-3"),
        });

        var handler = new GetPortalTicketsQueryHandler(repo);
        var result = await handler.Handle(new GetPortalTicketsQuery(owner), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data.Select(d => d.Reference).Should().BeEquivalentTo(new[] { "TKT-1", "TKT-2" });
    }

    [Fact]
    [Trait("AC", "406")]
    public async Task PJ9_Detail_ForOwnedTicket_IncludesMessagesAndSurveyFlag()
    {
        var owner = Guid.NewGuid();
        var message = TicketMessage.Create(Guid.NewGuid(), "Inbound", "WebForm", null, "hello", Guid.NewGuid());
        var ticket = OwnedTicket(owner, "TKT-1");

        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var messages = new Mock<IRepository<TicketMessage>>();
        messages.Setup(m => m.ListOrderedAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TicketMessage, bool>>>(), It.IsAny<System.Linq.Expressions.Expression<Func<TicketMessage, DateTime>>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TicketMessage> { message });

        var surveys = new Mock<IRepository<SurveyResponse>>();
        surveys.Setup(s => s.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SurveyResponse, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new GetPortalTicketDetailQueryHandler(tickets.Object, messages.Object, surveys.Object, new StubMessageFactory());

        var result = await handler.Handle(new GetPortalTicketDetailQuery(ticket.Id, owner), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Reference.Should().Be("TKT-1");
        result.Data.Messages.Should().ContainSingle().Which.Body.Should().Be("hello");
        result.Data.SurveySubmitted.Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "403")]
    public async Task PJ9_Detail_ForAnotherCustomer_ReturnsForbidden()
    {
        var owner = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var ticket = OwnedTicket(someoneElse, "TKT-9");

        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        var messages = new Mock<IRepository<TicketMessage>>();
        var surveys = new Mock<IRepository<SurveyResponse>>();

        var factory = new StubMessageFactory();
        var handler = new GetPortalTicketDetailQueryHandler(tickets.Object, messages.Object, surveys.Object, factory);

        var result = await handler.Handle(new GetPortalTicketDetailQuery(ticket.Id, owner), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.General.FORBIDDEN);
    }

    [Fact]
    [Trait("AC", "406")]
    public async Task PJ9_Detail_UnknownTicket_ReturnsNotFound()
    {
        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Ticket?)null);
        var messages = new Mock<IRepository<TicketMessage>>();
        var surveys = new Mock<IRepository<SurveyResponse>>();

        var factory = new StubMessageFactory();
        var handler = new GetPortalTicketDetailQueryHandler(tickets.Object, messages.Object, surveys.Object, factory);

        var result = await handler.Handle(new GetPortalTicketDetailQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Ticket.NOT_FOUND);
    }

    private sealed class StubMessageFactory : IMessageFactory
    {
        public Response<T> Success<T>(T data, string domainKey) => Response<T>.Ok(data, domainKey, "OK");
        public Response<T> Fail<T>(string domainKey, MessageType type) => Response<T>.Fail(domainKey, domainKey, type);
        public Response<T> Fail<T>(string domainKey, MessageType type, IList<FieldError> errors) => Response<T>.Fail(domainKey, domainKey, type, errors);
        public Response<T> NotFound<T>(string domainKey) => Response<T>.Fail(domainKey, domainKey, MessageType.NotFound);
        public Response<T> Validation<T>(string domainKey, IList<FieldError> errors) => Response<T>.Fail(domainKey, domainKey, MessageType.Validation, errors);
    }

    private sealed class FakeTicketRepository : IRepository<Ticket>
    {
        private readonly List<Ticket> _tickets;
        public FakeTicketRepository(IEnumerable<Ticket> tickets) => _tickets = tickets.ToList();

        public Task<IReadOnlyList<TDto>> ListProjectedOrderedAsync<TDto, TOrderKey>(
            System.Linq.Expressions.Expression<Func<Ticket, bool>>? predicate,
            System.Linq.Expressions.Expression<Func<Ticket, TDto>> selectExpression,
            System.Linq.Expressions.Expression<Func<Ticket, TOrderKey>> orderBy,
            bool descending,
            CancellationToken ct)
        {
            var compiled = predicate?.Compile() ?? (_ => true);
            var projected = _tickets.Where(compiled).Select(selectExpression.Compile()).ToList();
            return Task.FromResult((IReadOnlyList<TDto>)projected);
        }

        public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Ticket?> GetTrackedAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Ticket?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<Ticket, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(System.Linq.Expressions.Expression<Func<Ticket, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Ticket>> ListAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Ticket>> ListAsync(System.Linq.Expressions.Expression<Func<Ticket, bool>>? predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Ticket>> ListOrderedAsync<TOrderKey>(System.Linq.Expressions.Expression<Func<Ticket, bool>>? predicate, System.Linq.Expressions.Expression<Func<Ticket, TOrderKey>> orderBy, bool descending, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TDto>> ListProjectedAsync<TDto>(System.Linq.Expressions.Expression<Func<Ticket, bool>>? predicate, System.Linq.Expressions.Expression<Func<Ticket, TDto>> selectExpression, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, System.Linq.Expressions.Expression<Func<Ticket, bool>>? filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, System.Linq.Expressions.Expression<Func<Ticket, bool>>? filter, System.Linq.Expressions.Expression<Func<Ticket, TDto>> selectExpression, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Ticket entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Ticket> entities, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(Ticket entity) => throw new NotSupportedException();
        public void SetOriginalValue(Ticket entity, string propertyName, object? value) => throw new NotSupportedException();
        public void Remove(Ticket entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<Ticket> entities) => throw new NotSupportedException();
        public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<Ticket, bool>>? predicate = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}