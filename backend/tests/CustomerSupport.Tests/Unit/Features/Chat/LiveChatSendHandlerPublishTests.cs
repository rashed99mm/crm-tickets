using System.Linq.Expressions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Chat;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Channels;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.Common;
using CustomerSupport.Shared.Contracts;
using CustomerSupport.Shared.Contracts.Messages;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Chat;

/// <summary>
/// CC-30 / CC-34 — the live-chat send handlers publish a <see cref="ChatMessagePushed"/> (rather
/// than pushing directly) after the message is persisted. The bus consumer is the single source of
/// the real-time push, which is what lets a message cross the InternalApi/ExternalApi boundary.
/// </summary>
public class LiveChatSendHandlerPublishTests
{
    private readonly Mock<IRepository<LiveChatSession>> _sessions = new();
    private readonly Mock<IRepository<LiveChatMessage>> _chatMessages = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMessagePublisher> _publisher = new();
    private readonly StubMessageFactory _messages = new();

    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Mock<IIdentityUserService> _users = new();
    private readonly Mock<IUserContext> _user = new();

    public LiveChatSendHandlerPublishTests()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _user.Setup(u => u.UserId).Returns(_agentId);
        _user.Setup(u => u.Email).Returns("agent@cce-platform.com");
        _users.Setup(u => u.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, CancellationToken _) => null);
    }

    private static LiveChatSession ActiveSession(Guid agentId)
    {
        var (session, _) = LiveChatSession.Start("Sara", "sara@example.com");
        // EF sets Id on materialisation; for the unit test grant it a real id so the push carries it.
        session.Claim(agentId);
        return session;
    }

    [Fact]
    [Trait("AC", "CC-30")]
    public async Task AgentSend_PublishesChatMessagePushed_WithPersistedMessage()
    {
        var session = ActiveSession(_agentId);
        _sessions.Setup(s => s.GetTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _chatMessages.Setup(m => m.AddAsync(It.IsAny<LiveChatMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new SendAgentChatMessageCommandHandler(
            _sessions.Object, _chatMessages.Object, _users.Object, _user.Object, _unitOfWork.Object,
            _publisher.Object, _messages);

        var result = await handler.Handle(new SendAgentChatMessageCommand(Guid.Empty, "A refund was issued."), CancellationToken.None);

        result.Success.Should().BeTrue();
        _publisher.Verify(
            p => p.PublishAsync(Topics.ChatMessagesPushed, It.Is<ChatMessagePushed>(x => x.SessionId == session.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("AC", "CC-30")]
    public async Task AgentSend_PushesOnlyAfterSave()
    {
        var session = ActiveSession(_agentId);
        _sessions.Setup(s => s.GetTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _chatMessages.Setup(m => m.AddAsync(It.IsAny<LiveChatMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new SendAgentChatMessageCommandHandler(
            _sessions.Object, _chatMessages.Object, _users.Object, _user.Object, _unitOfWork.Object,
            _publisher.Object, _messages);

        await handler.Handle(new SendAgentChatMessageCommand(Guid.Empty, "A refund was issued."), CancellationToken.None);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(
            p => p.PublishAsync(It.IsAny<string>(), It.IsAny<ChatMessagePushed>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("AC", "CC-34")]
    public async Task AnonymousSend_PublishesChatMessagePushed()
    {
        var session = LiveChatSession.Start("Sara", "sara@example.com").Session;
        _sessions.Setup(s => s.FirstOrDefaultAsync(It.IsAny<Expression<Func<LiveChatSession, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _sessions.Setup(s => s.GetTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _chatMessages.Setup(m => m.AddAsync(It.IsAny<LiveChatMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new SendAnonymousChatMessageCommandHandler(
            _sessions.Object, _chatMessages.Object, _unitOfWork.Object, _publisher.Object, _messages);

        var result = await handler.Handle(new SendAnonymousChatMessageCommand("opaque-token", "Hello agent"), CancellationToken.None);

        result.Success.Should().BeTrue();
        _publisher.Verify(
            p => p.PublishAsync(Topics.ChatMessagesPushed, It.Is<ChatMessagePushed>(x => x.SessionId == session.Id), It.IsAny<CancellationToken>()),
            Times.Once);
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
