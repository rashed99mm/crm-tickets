using System.Linq.Expressions;
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Ai.Chat;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Ai;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Ai.Chat;

/// <summary>
/// AI-38/39 — a conversation is only made durable once the assistant answer was generated
/// successfully. Prior to the fix, <see cref="AiChatService.AnswerAsync"/> queued both turns but
/// never flushed them, and <see cref="StartAiChatCommandHandler"/> never even added the session
/// row — so a follow-up Send or a handoff could not find the conversation.
/// </summary>
public class AiChatPersistenceTests
{
    [Fact]
    public async Task Answer_Success_PersistsOnceWithBothTurns()
    {
        var uow = new Mock<IUnitOfWork>();
        var turns = new RecordingMessageRepository();
        var contents = new FakeContentRepository([]);
        var ai = new StubAiService { AnswerBody = "Reset it in settings." };
        var chat = new AiChatService(
            new Mock<IRepository<AiChatSession>>().Object,
            turns,
            contents,
            ai,
            uow.Object,
            new StubMessageFactory());

        var session = AiChatSession.Create(Guid.NewGuid(), AiChatScope.Staff);
        var response = await chat.AnswerAsync(session, "How do I reset my password?", CancellationToken.None);

        response.Success.Should().BeTrue();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        turns.Added.Should().HaveCount(2);
        turns.Added.Should().Contain(t => t.Role == AiChatRole.User);
        turns.Added.Should().Contain(t => t.Role == AiChatRole.Assistant);
    }

    [Fact]
    public async Task Answer_ProviderFails_NothingPersisted()
    {
        var uow = new Mock<IUnitOfWork>();
        var turns = new RecordingMessageRepository();
        var contents = new FakeContentRepository([]);
        var ai = new StubAiService { FailAnswer = true };
        var chat = new AiChatService(
            new Mock<IRepository<AiChatSession>>().Object,
            turns,
            contents,
            ai,
            uow.Object,
            new StubMessageFactory());

        var session = AiChatSession.Create(Guid.NewGuid(), AiChatScope.Staff);
        var response = await chat.AnswerAsync(session, "How do I reset my password?", CancellationToken.None);

        response.Success.Should().BeFalse();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        // The failure short-circuits before the assistant turn is produced; nothing is flushed to
        // disk even though the user turn was tracked in memory.
        turns.Added.Should().NotContain(t => t.Role == AiChatRole.Assistant);
    }

    [Fact]
    public async Task Start_Session_IsAddedToRepository()
    {
        var sessions = new Mock<IRepository<AiChatSession>>();
        sessions.Setup(s => s.AddAsync(It.IsAny<AiChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        var turns = new RecordingMessageRepository();
        var contents = new FakeContentRepository([]);
        var ai = new StubAiService { AnswerBody = "Reset it in settings." };
        var user = new Mock<IUserContext>();
        user.Setup(u => u.IsAuthenticated).Returns(true);
        user.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var chat = new AiChatService(
            sessions.Object, turns, contents, ai, uow.Object, new StubMessageFactory());
        var handler = new StartAiChatCommandHandler(sessions.Object, chat, user.Object, new StubMessageFactory());

        var response = await handler.Handle(new StartAiChatCommand("How do I reset my password?", AiChatScope.Staff), CancellationToken.None);

        response.Success.Should().BeTrue();
        sessions.Verify(s => s.AddAsync(It.IsAny<AiChatSession>(), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Records every turn handed to <c>AddAsync</c> and executes the projections used to render history.</summary>
    private sealed class RecordingMessageRepository : IRepository<AiChatMessage>
    {
        private readonly List<AiChatMessage> _persisted = new();
        public IReadOnlyList<AiChatMessage> Added => _persisted;

        public Task AddAsync(AiChatMessage entity, CancellationToken ct = default)
        {
            _persisted.Add(entity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TDto>> ListProjectedOrderedAsync<TDto, TOrderKey>(
            Expression<Func<AiChatMessage, bool>>? predicate,
            Expression<Func<AiChatMessage, TDto>> selectExpression,
            Expression<Func<AiChatMessage, TOrderKey>> orderBy,
            bool descending,
            CancellationToken ct = default)
        {
            var compiled = predicate?.Compile() ?? (_ => true);
            var projected = _persisted.Where(compiled).Select(selectExpression.Compile()).ToList();
            return Task.FromResult((IReadOnlyList<TDto>)projected);
        }

        public Task<IReadOnlyList<TDto>> ListProjectedAsync<TDto>(
            Expression<Func<AiChatMessage, bool>>? predicate,
            Expression<Func<AiChatMessage, TDto>> selectExpression,
            CancellationToken ct = default)
        {
            var compiled = predicate?.Compile() ?? (_ => true);
            return Task.FromResult((IReadOnlyList<TDto>)_persisted.Where(compiled).Select(selectExpression.Compile()).ToList());
        }

        public Task<AiChatMessage?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiChatMessage?> GetTrackedAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AiChatMessage?> FirstOrDefaultAsync(Expression<Func<AiChatMessage, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Expression<Func<AiChatMessage, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiChatMessage>> ListAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiChatMessage>> ListAsync(Expression<Func<AiChatMessage, bool>>? predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiChatMessage>> ListOrderedAsync<TOrderKey>(Expression<Func<AiChatMessage, bool>>? predicate, Expression<Func<AiChatMessage, TOrderKey>> orderBy, bool descending, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, Expression<Func<AiChatMessage, bool>>? filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, Expression<Func<AiChatMessage, bool>>? filter, Expression<Func<AiChatMessage, TDto>> selectExpression, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<AiChatMessage> entities, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(AiChatMessage entity) => throw new NotSupportedException();
        public void SetOriginalValue(AiChatMessage entity, string propertyName, object? value) => throw new NotSupportedException();
        public void Remove(AiChatMessage entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<AiChatMessage> entities) => throw new NotSupportedException();
        public Task<int> CountAsync(Expression<Func<AiChatMessage, bool>>? predicate = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeContentRepository : IRepository<Content>
    {
        private readonly List<Content> _contents;
        public FakeContentRepository(IEnumerable<Content> contents) => _contents = contents.ToList();

        public Task<IReadOnlyList<TDto>> ListProjectedAsync<TDto>(
            Expression<Func<Content, bool>>? predicate,
            Expression<Func<Content, TDto>> selectExpression,
            CancellationToken ct = default)
        {
            var compiled = predicate?.Compile() ?? (_ => true);
            return Task.FromResult((IReadOnlyList<TDto>)_contents.Where(compiled).Select(selectExpression.Compile()).ToList());
        }

        public Task<Content?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Content?> GetTrackedAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Content?> FirstOrDefaultAsync(Expression<Func<Content, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Expression<Func<Content, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Content>> ListAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Content>> ListAsync(Expression<Func<Content, bool>>? predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Content>> ListOrderedAsync<TOrderKey>(Expression<Func<Content, bool>>? predicate, Expression<Func<Content, TOrderKey>> orderBy, bool descending, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TDto>> ListProjectedOrderedAsync<TDto, TOrderKey>(Expression<Func<Content, bool>>? predicate, Expression<Func<Content, TDto>> selectExpression, Expression<Func<Content, TOrderKey>> orderBy, bool descending, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, Expression<Func<Content, bool>>? filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaginatedList<TDto>> GetPagedAsync<TDto>(BasePagedQuery pagedQuery, Expression<Func<Content, bool>>? filter, Expression<Func<Content, TDto>> selectExpression, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Content entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Content> entities, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(Content entity) => throw new NotSupportedException();
        public void SetOriginalValue(Content entity, string propertyName, object? value) => throw new NotSupportedException();
        public void Remove(Content entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<Content> entities) => throw new NotSupportedException();
        public Task<int> CountAsync(Expression<Func<Content, bool>>? predicate = null, CancellationToken ct = default) => throw new NotSupportedException();
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
        public string AnswerBody { get; set; } = "answer";
        public bool FailAnswer { get; set; }
        public bool IsAvailable => true;

        public Task<AiOutcome<string>> AnswerAsync(string question, IReadOnlyList<KbPassage> passages, CancellationToken ct) =>
            Task.FromResult(FailAnswer ? AiOutcome<string>.Fail("provider failed") : AiOutcome<string>.Ok(AnswerBody));

        public Task<AiOutcome<string>> SummariseAsync(string threadText, CancellationToken ct) => throw new NotSupportedException();
        public Task<AiOutcome<string?>> ClassifySentimentAsync(string threadText, CancellationToken ct) => throw new NotSupportedException();
        public Task<AiOutcome<string>> DraftReplyAsync(string threadText, string? extraInstruction, CancellationToken ct) => throw new NotSupportedException();
        public Task<AiOutcome<IReadOnlyList<string>>> SuggestCategoriesAsync(string threadText, IReadOnlyList<string> categoryNames, CancellationToken ct) => throw new NotSupportedException();
        public Task<AiOutcome<IReadOnlyList<KbCitation>>> SuggestSolutionsAsync(string question, IReadOnlyList<KbPassage> candidates, CancellationToken ct) => throw new NotSupportedException();
    }
}
