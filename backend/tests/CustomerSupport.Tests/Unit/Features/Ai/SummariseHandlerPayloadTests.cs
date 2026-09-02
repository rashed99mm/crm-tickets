using System.Linq.Expressions;
using System.Text.Json;
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Ai;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Ai;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Ai;

/// <summary>
/// AC-21.11 — the <see cref="SummariseTicketCommandHandler"/> persists <c>{ text, sentiment }</c>
/// to <c>AiSuggestions.Payload</c>, and a sentiment failure falls through to <c>sentiment: null</c>
/// rather than failing the summary (A5).
/// </summary>
public class SummariseHandlerPayloadTests
{
    [Fact]
    [Trait("AC", "21.11")]
    public async Task Handler_PersistsTextAndSentiment()
    {
        var (handler, suggestions, ticket) = Arrange(
            summaryText: "Customer cannot sign in.",
            sentiment: "Frustrated");

        var response = await handler.Handle(new SummariseTicketCommand(ticket.Id), CancellationToken.None);

        response.Success.Should().BeTrue();
        var payload = CapturedPayload(suggestions);
        payload.GetProperty("text").GetString().Should().Be("Customer cannot sign in.");
        payload.GetProperty("sentiment").GetString().Should().Be("Frustrated");
    }

    [Fact]
    [Trait("AC", "21.11")]
    public async Task Handler_SentimentFailure_StillSucceedsWithNullSentiment()
    {
        var (handler, suggestions, ticket) = Arrange(
            summaryText: "Customer cannot sign in.",
            sentiment: null);

        var response = await handler.Handle(new SummariseTicketCommand(ticket.Id), CancellationToken.None);

        response.Success.Should().BeTrue();
        var payload = CapturedPayload(suggestions);
        payload.GetProperty("text").GetString().Should().Be("Customer cannot sign in.");
        payload.GetProperty("sentiment").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    [Trait("AC", "21.11")]
    public async Task Handler_GarbageSentiment_TreatsAsNull()
    {
        // A malformed or unknown label must not crash the summary; it falls through to "no chip".
        var (handler, suggestions, ticket) = Arrange(
            summaryText: "Customer cannot sign in.",
            sentiment: "Ecstatic");

        var response = await handler.Handle(new SummariseTicketCommand(ticket.Id), CancellationToken.None);

        response.Success.Should().BeTrue();
        CapturedPayload(suggestions).GetProperty("sentiment").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    [Trait("AC", "21.11")]
    public async Task Handler_ShortThread_ReturnsTooShort()
    {
        // The summary-skip path is unchanged by the sentiment addition; this guards against a
        // regression where a sentiment call runs before the short-thread check.
        var (handler, _, ticket) = Arrange(
            summaryText: "ignored",
            sentiment: "Neutral",
            threadSize: 1);

        var response = await handler.Handle(new SummariseTicketCommand(ticket.Id), CancellationToken.None);

        response.Success.Should().BeFalse();
        response.Code.Should().Be("AI_THREAD_TOO_SHORT");
    }

    private static (SummariseTicketCommandHandler, Mock<IRepository<AiSuggestion>>, Ticket) Arrange(
        string summaryText, string? sentiment, int threadSize = 3)
    {
        var userId = Guid.NewGuid();
        var ticket = Ticket.Create(
            "TKT-1", "Subject", "Description", Guid.NewGuid(), Guid.NewGuid(), "Medium", "Medium", userId);
        ticket.AssignTo(userId, userId);

        var messages = new FakeMessageRepository(Enumerable.Range(0, threadSize)
            .Select(i => TicketMessage.Create(ticket.Id, "Inbound", "Email", null, $"body {i}", userId))
            .ToList());

        var suggestions = new Mock<IRepository<AiSuggestion>>();
        suggestions.Setup(s => s.AddAsync(It.IsAny<AiSuggestion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var ai = new StubAiService
        {
            SummaryResult = summaryText,
            SentimentResult = sentiment,
        };

        var user = new Mock<IUserContext>();
        user.Setup(u => u.UserId).Returns(userId);
        user.Setup(u => u.HasAnyRole(It.IsAny<string[]>())).Returns(true);

        var messages2 = new StubMessageFactory();

        var handler = new SummariseTicketCommandHandler(
            tickets.Object, messages, suggestions.Object, ai, user.Object,
            new Mock<IUnitOfWork>().Object, messages2);
        return (handler, suggestions, ticket);
    }

    private static JsonElement CapturedPayload(Mock<IRepository<AiSuggestion>> suggestions)
    {
        suggestions.Verify(
            s => s.AddAsync(It.IsAny<AiSuggestion>(), It.IsAny<CancellationToken>()),
            Times.Once);
        var captured = (AiSuggestion)suggestions.Invocations
            .First(i => i.Method.Name == nameof(IRepository<AiSuggestion>.AddAsync))
            .Arguments[0]!;
        return JsonDocument.Parse(captured.Payload).RootElement;
    }

    /// <summary>
    /// Hand-rolled fake for <see cref="IRepository{TicketMessage}"/>. The real repository runs the
    /// selector expression in-process; this fake does the same, so the handler's projection
    /// (<c>m => new { m.Body }</c>) is exercised rather than stubbed.
    /// </summary>
    private sealed class FakeMessageRepository : IRepository<TicketMessage>
    {
        private readonly List<TicketMessage> _messages;
        public FakeMessageRepository(IEnumerable<TicketMessage> messages) => _messages = messages.ToList();

        public Task<IReadOnlyList<TDto>> ListProjectedAsync<TDto>(
            Expression<Func<TicketMessage, bool>>? predicate,
            Expression<Func<TicketMessage, TDto>> selectExpression,
            CancellationToken ct = default)
        {
            var compiled = predicate?.Compile() ?? (_ => true);
            var projected = _messages.Where(compiled).Select(selectExpression.Compile()).ToList();
            return Task.FromResult((IReadOnlyList<TDto>)projected);
        }

        public Task<TicketMessage?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TicketMessage?> GetTrackedAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TicketMessage?> FirstOrDefaultAsync(Expression<Func<TicketMessage, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Expression<Func<TicketMessage, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TicketMessage>> ListAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TicketMessage>> ListAsync(Expression<Func<TicketMessage, bool>>? predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TicketMessage>> ListOrderedAsync<TOrderKey>(Expression<Func<TicketMessage, bool>>? predicate, Expression<Func<TicketMessage, TOrderKey>> orderBy, bool descending, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TDto>> ListProjectedOrderedAsync<TDto, TOrderKey>(Expression<Func<TicketMessage, bool>>? predicate, Expression<Func<TicketMessage, TDto>> selectExpression, Expression<Func<TicketMessage, TOrderKey>> orderBy, bool descending, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, Expression<Func<TicketMessage, bool>>? filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, Expression<Func<TicketMessage, bool>>? filter, Expression<Func<TicketMessage, TDto>> selectExpression, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(TicketMessage entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<TicketMessage> entities, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(TicketMessage entity) => throw new NotSupportedException();
        public void SetOriginalValue(TicketMessage entity, string propertyName, object? value) => throw new NotSupportedException();
        public void Remove(TicketMessage entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<TicketMessage> entities) => throw new NotSupportedException();
        public Task<int> CountAsync(Expression<Func<TicketMessage, bool>>? predicate = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// In-process test stub for <see cref="IMessageFactory"/>. Mirrors <c>CreateTicketCommandHandlerTests.StubMessageFactory</c>
    /// but uses the real <see cref="Response{T}"/> constructors so the handler's success/fail
    /// branches return the right shape.
    /// </summary>
    private sealed class StubMessageFactory : IMessageFactory
    {
        public Response<T> Success<T>(T data, string domainKey) => Response<T>.Ok(data, domainKey, "ok");
        public Response<T> Fail<T>(string domainKey, MessageType type) => Response<T>.Fail(domainKey, domainKey, type);
        public Response<T> Fail<T>(string domainKey, MessageType type, IList<FieldError> errors) => Response<T>.Fail(domainKey, domainKey, type, errors);
        public Response<T> NotFound<T>(string domainKey) => Response<T>.Fail(domainKey, domainKey, MessageType.NotFound);
        public Response<T> Validation<T>(string domainKey, IList<FieldError> errors) => Response<T>.Fail(domainKey, domainKey, MessageType.Validation, errors);
    }

    /// <summary>
    /// In-process test stub for <see cref="IAiService"/>. The handler's tests need to vary the
    /// summary text and sentiment label; a stub is more readable than Moq here and keeps the
    /// assertion logic in the test bodies.
    /// </summary>
    private sealed class StubAiService : IAiService
    {
        public string SummaryResult { get; set; } = "summary";
        public string? SentimentResult { get; set; } = "Neutral";

        public bool IsAvailable => true;

        public Task<AiOutcome<string>> SummariseAsync(string threadText, CancellationToken ct) =>
            Task.FromResult(AiOutcome<string>.Ok(SummaryResult));

        public Task<AiOutcome<string?>> ClassifySentimentAsync(string threadText, CancellationToken ct) =>
            Task.FromResult(SentimentResult is null
                ? AiOutcome<string?>.Fail("sentiment unavailable")
                : AiOutcome<string?>.Ok(SentimentResult));

        public Task<AiOutcome<string>> DraftReplyAsync(string threadText, string? extraInstruction, CancellationToken ct) =>
            Task.FromResult(AiOutcome<string>.Ok("draft"));

        public Task<AiOutcome<IReadOnlyList<string>>> SuggestCategoriesAsync(
            string threadText, IReadOnlyList<string> categoryNames, CancellationToken ct) =>
            Task.FromResult(AiOutcome<IReadOnlyList<string>>.Ok(categoryNames));

        public Task<AiOutcome<IReadOnlyList<KbCitation>>> SuggestSolutionsAsync(
            string question, IReadOnlyList<KbPassage> candidates, CancellationToken ct) =>
            Task.FromResult(AiOutcome<IReadOnlyList<KbCitation>>.Ok([]));

        public Task<AiOutcome<string>> AnswerAsync(string question, IReadOnlyList<KbPassage> passages, CancellationToken ct) =>
            Task.FromResult(AiOutcome<string>.Ok("answer"));
    }
}
