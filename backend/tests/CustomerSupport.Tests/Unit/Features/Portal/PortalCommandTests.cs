using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Features.Portal.Commands.CreatePortalReply;
using CustomerSupport.Application.Features.Portal.Commands.SubmitSurvey;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Survey;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Portal;

public class PortalCommandTests
{
    private static Ticket OwnedTicket(Guid customerId, string status = "Open") =>
        Ticket.Create("TKT-1", "Subject", "Description", customerId, Guid.NewGuid(), "Low", Guid.NewGuid());

    [Fact]
    [Trait("AC", "407")]
    public async Task PJ10_Reply_OnOwnedTicket_CreatesInboundPortalMessage()
    {
        var owner = Guid.NewGuid();
        var ticket = OwnedTicket(owner);
        var captured = new List<TicketMessage>();

        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        var messages = new Mock<IRepository<TicketMessage>>();
        messages.Setup(m => m.AddAsync(It.IsAny<TicketMessage>(), It.IsAny<CancellationToken>()))
            .Callback<TicketMessage, CancellationToken>((m, _) => captured.Add(m));
        var userContext = new Mock<IUserContext>();
        userContext.Setup(u => u.UserId).Returns(Guid.NewGuid());
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new CreatePortalReplyCommandHandler(tickets.Object, messages.Object, userContext.Object, unitOfWork.Object, new StubMessageFactory());
        var result = await handler.Handle(new CreatePortalReplyCommand(ticket.Id, "thanks", owner), CancellationToken.None);

        result.Success.Should().BeTrue();
        captured.Should().ContainSingle();
        captured[0].Direction.Should().Be("Inbound");
        captured[0].Channel.Should().Be("Portal");
        captured[0].Body.Should().Be("thanks");
    }

    [Fact]
    [Trait("AC", "403")]
    public async Task PJ10_Reply_OnAnotherCustomerTicket_ReturnsForbidden()
    {
        var owner = Guid.NewGuid();
        var ticket = OwnedTicket(owner);

        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        var messages = new Mock<IRepository<TicketMessage>>();
        var userContext = new Mock<IUserContext>();
        userContext.Setup(u => u.UserId).Returns(Guid.NewGuid());
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new CreatePortalReplyCommandHandler(tickets.Object, messages.Object, userContext.Object, unitOfWork.Object, new StubMessageFactory());
        var result = await handler.Handle(new CreatePortalReplyCommand(ticket.Id, "hi", Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.General.FORBIDDEN);
    }

    [Fact]
    [Trait("AC", "407")]
    public async Task PJ10_Reply_UnknownTicket_ReturnsNotFound()
    {
        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Ticket?)null);
        var messages = new Mock<IRepository<TicketMessage>>();
        var userContext = new Mock<IUserContext>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new CreatePortalReplyCommandHandler(tickets.Object, messages.Object, userContext.Object, unitOfWork.Object, new StubMessageFactory());
        var result = await handler.Handle(new CreatePortalReplyCommand(Guid.NewGuid(), "hi", Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Ticket.NOT_FOUND);
    }

    [Fact]
    [Trait("AC", "408")]
    public async Task PJ11_Survey_OnResolvedOwnedTicket_CreatesResponse()
    {
        var owner = Guid.NewGuid();
        var ticket = OwnedTicket(owner);
        SetStatus(ticket, TicketStatus.Resolved.Value);
        var captured = new List<SurveyResponse>();

        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        var surveys = new Mock<IRepository<SurveyResponse>>();
        surveys.Setup(s => s.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SurveyResponse, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        surveys.Setup(s => s.AddAsync(It.IsAny<SurveyResponse>(), It.IsAny<CancellationToken>()))
            .Callback<SurveyResponse, CancellationToken>((s, _) => captured.Add(s));
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new SubmitSurveyCommandHandler(tickets.Object, surveys.Object, unitOfWork.Object, new StubMessageFactory());
        var result = await handler.Handle(new SubmitSurveyCommand(ticket.Id, 5, "great", owner), CancellationToken.None);

        result.Success.Should().BeTrue();
        captured.Should().ContainSingle();
        captured[0].Rating.Should().Be(5);
        captured[0].FreeText.Should().Be("great");
    }

    [Fact]
    [Trait("AC", "409")]
    public async Task PJ11_Survey_OnUnresolvedTicket_ReturnsNotResolved()
    {
        var owner = Guid.NewGuid();
        var ticket = OwnedTicket(owner); // status Open
        SetStatus(ticket, TicketStatus.Open.Value);

        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        var surveys = new Mock<IRepository<SurveyResponse>>();
        surveys.Setup(s => s.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SurveyResponse, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new SubmitSurveyCommandHandler(tickets.Object, surveys.Object, unitOfWork.Object, new StubMessageFactory());
        var result = await handler.Handle(new SubmitSurveyCommand(ticket.Id, 4, null, owner), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Survey.TICKET_NOT_RESOLVED);
    }

    [Fact]
    [Trait("AC", "408")]
    public async Task PJ11_Survey_Duplicate_ReturnsConflict()
    {
        var owner = Guid.NewGuid();
        var ticket = OwnedTicket(owner);
        SetStatus(ticket, TicketStatus.Resolved.Value);

        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        var surveys = new Mock<IRepository<SurveyResponse>>();
        surveys.Setup(s => s.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SurveyResponse, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new SubmitSurveyCommandHandler(tickets.Object, surveys.Object, unitOfWork.Object, new StubMessageFactory());
        var result = await handler.Handle(new SubmitSurveyCommand(ticket.Id, 5, null, owner), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Survey.ALREADY_SUBMITTED);
    }

    private static void SetStatus(Ticket ticket, string status)
    {
        // ChangeStatus enforces the transition table, so drive New -> Open -> target.
        if (status != TicketStatus.Open.Value)
        {
            ticket.ChangeStatus(TicketStatus.Open.Value, Guid.NewGuid());
        }

        ticket.ChangeStatus(status, Guid.NewGuid());
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