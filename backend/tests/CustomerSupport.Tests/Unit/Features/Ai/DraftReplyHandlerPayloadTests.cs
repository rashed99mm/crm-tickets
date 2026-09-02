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
/// AC-21.12 — the <see cref="DraftReplyCommandHandler"/> persists a <c>drafts</c> array to
/// <c>AiSuggestions.Payload</c>. One model call returns up to three drafts; a structured answer
/// that lost its items is a safe failure (AI-36), not a draft of "".
/// </summary>
public class DraftReplyHandlerPayloadTests
{
    [Fact]
    [Trait("AC", "21.12")]
    public async Task Handler_PersistsDraftsArray()
    {
        var (handler, suggestions, ticket) = Arrange(
            modelJson: """{"items":["First reply.","Second reply.","Third reply."]}""");

        var response = await handler.Handle(new DraftReplyCommand(ticket.Id), CancellationToken.None);

        response.Success.Should().BeTrue();
        var payload = CapturedPayload(suggestions);
        var drafts = payload.GetProperty("drafts").EnumerateArray().Select(e => e.GetString()).ToList();
        drafts.Should().BeEquivalentTo(new[] { "First reply.", "Second reply.", "Third reply." },
            opts => opts.WithStrictOrdering());
    }

    [Fact]
    [Trait("AC", "21.12")]
    public async Task Handler_FewerThanThreeDrafts_PersistsWhatItGot()
    {
        var (handler, suggestions, ticket) = Arrange(modelJson: """{"items":["Only one."]}""");

        var response = await handler.Handle(new DraftReplyCommand(ticket.Id), CancellationToken.None);

        response.Success.Should().BeTrue();
        var drafts = CapturedPayload(suggestions).GetProperty("drafts");
        drafts.GetArrayLength().Should().Be(1);
        drafts[0].GetString().Should().Be("Only one.");
    }

    [Fact]
    [Trait("AC", "21.12")]
    public async Task Handler_ProviderFailure_ReturnsProviderFailed()
    {
        var (handler, _, ticket) = Arrange(modelJson: null, providerFails: true);

        var response = await handler.Handle(new DraftReplyCommand(ticket.Id), CancellationToken.None);

        response.Success.Should().BeFalse();
        response.Code.Should().Be("AI_PROVIDER_FAILED");
    }

    [Fact]
    [Trait("AC", "21.12")]
    public async Task Handler_EmptyOrMalformedJson_ReturnsProviderFailed()
    {
        // AI-36 — a malformed model answer is a safe failure, never a draft of "".
        var (handler, _, ticket) = Arrange(modelJson: "not json at all");

        var response = await handler.Handle(new DraftReplyCommand(ticket.Id), CancellationToken.None);

        response.Success.Should().BeFalse();
        response.Code.Should().Be("AI_PROVIDER_FAILED");
    }

    private static (DraftReplyCommandHandler, Mock<IRepository<AiSuggestion>>, Ticket) Arrange(
        string? modelJson, bool providerFails = false)
    {
        var userId = Guid.NewGuid();
        var ticket = Ticket.Create(
            "TKT-2", "Subject", "Description", Guid.NewGuid(), Guid.NewGuid(), "Medium", "Medium", userId);
        ticket.AssignTo(userId, userId);

        var messages = new FakeMessageRepository(Enumerable.Range(0, 3)
            .Select(i => TicketMessage.Create(ticket.Id, "Inbound", "Email", null, $"body {i}", userId))
            .ToList());

        var suggestions = new Mock<IRepository<AiSuggestion>>();
        suggestions.Setup(s => s.AddAsync(It.IsAny<AiSuggestion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tickets = new Mock<IRepository<Ticket>>();
        tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var ai = new StubAiService
        {
            DraftsResult = modelJson,
            ProviderFails = providerFails,
        };

        var user = new Mock<IUserContext>();
        user.Setup(u => u.UserId).Returns(userId);
        user.Setup(u => u.HasAnyRole(It.IsAny<string[]>())).Returns(true);

        var handler = new DraftReplyCommandHandler(
            tickets.Object, messages, suggestions.Object, ai, user.Object,
            new Mock<IUnitOfWork>().Object, new StubMessageFactory());
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

    private sealed class StubMessageFactory : IMessageFactory
    {
        public Response<T> Success<T>(T data, string domainKey) => Response<T>.Ok(data, domainKey, "ok");
        public Response<T> Fail<T>(string domainKey, MessageType type) => Response<T>.Fail(domainKey, domainKey, type);
        public Response<T> Fail<T>(string domainKey, MessageType type, IList<FieldError> errors) => Response<T>.Fail(domainKey, domainKey, type, errors);
        public Response<T> NotFound<T>(string domainKey) => Response<T>.Fail(domainKey, domainKey, MessageType.NotFound);
        public Response<T> Validation<T>(string domainKey, IList<FieldError> errors) => Response<T>.Fail(domainKey, domainKey, MessageType.Validation, errors);
    }

    private sealed class StubAiService : IAiService
    {
        public string? DraftsResult { get; set; }
        public bool ProviderFails { get; set; }

        public bool IsAvailable => true;

        public Task<AiOutcome<string>> SummariseAsync(string threadText, CancellationToken ct) =>
            Task.FromResult(AiOutcome<string>.Ok("summary"));

        public Task<AiOutcome<string?>> ClassifySentimentAsync(string threadText, CancellationToken ct) =>
            Task.FromResult(AiOutcome<string?>.Ok("Neutral"));

        public Task<AiOutcome<string>> DraftReplyAsync(string threadText, string? extraInstruction, CancellationToken ct)
        {
            if (ProviderFails)
            {
                return Task.FromResult(AiOutcome<string>.Fail("provider down"));
            }
            return Task.FromResult(AiOutcome<string>.Ok(DraftsResult ?? ""));
        }

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
