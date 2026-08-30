using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Ai;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Shared.Contracts;
using CustomerSupport.Shared.Contracts.Messages;
using CustomerSupport.Domain.Entities.Channels;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.Chat;

public sealed record ChatSessionDto(
    Guid Id,
    string? CustomerName,
    string? CustomerEmail,
    string Status,
    string Priority,
    string Type,
    DateTime CreatedAt,
    DateTime? ClaimedAt,
    Guid? ClaimedByAgentId,
    string? ClaimedByAgentName,
    DateTime? ClosedAt);

public sealed record ChatMessageDto(
    Guid Id,
    Guid SessionId,
    string SenderType,
    string SenderName,
    Guid? SenderId,
    string Body,
    DateTime SentAt);

public sealed record StartChatSessionResponse(string SessionToken, Guid SessionId);

public sealed record StartChatSessionRequest(string? CustomerName, string? CustomerEmail, string? InitialMessage);

public sealed record SendLiveChatMessageRequest(string Body);

public sealed record ChatReplySuggestionDto(IReadOnlyList<string> Drafts, string Summary);

public sealed class ListWaitingChatSessionsQuery : BasePagedQuery, IQuery<Response<PaginatedList<ChatSessionDto>>>
{
    public string? Status { get; init; }
    public string? Search { get; init; }
}
public sealed record ClaimChatSessionCommand(Guid SessionId) : ICommand<Response<ChatSessionDto>>;
public sealed record GetChatTranscriptQuery(Guid SessionId) : IQuery<Response<IReadOnlyList<ChatMessageDto>>>;
public sealed record SendAgentChatMessageCommand(Guid SessionId, string Body) : ICommand<Response<ChatMessageDto>>;
public sealed record CloseChatSessionCommand(Guid SessionId) : ICommand<Response<Unit>>;
public sealed record StartAnonymousChatSessionCommand(string? CustomerName, string? CustomerEmail, string? InitialMessage)
    : ICommand<Response<StartChatSessionResponse>>;
public sealed record SendAnonymousChatMessageCommand(string Token, string Body) : ICommand<Response<ChatMessageDto>>;
public sealed record GetAnonymousChatTranscriptQuery(string Token) : IQuery<Response<IReadOnlyList<ChatMessageDto>>>;
public sealed record SuggestChatReplyCommand(Guid SessionId) : ICommand<Response<ChatReplySuggestionDto>>;

internal static class LiveChatMapping
{
    public static ChatSessionDto ToDto(LiveChatSession session, string? agentName = null)
    {
        var priority = ComputePriority(session);
        return new ChatSessionDto(
            session.Id,
            session.CustomerName,
            session.CustomerEmail,
            session.Status,
            priority,
            "Chat",
            session.CreatedAt,
            session.ClaimedAt,
            session.ClaimedByAgentId,
            agentName,
            session.ClosedAt);
    }

    private static string ComputePriority(LiveChatSession session)
    {
        if (session.Status == "Waiting")
        {
            var waitMinutes = (DateTime.UtcNow - session.CreatedAt).TotalMinutes;
            if (waitMinutes > 30) return "Urgent";
            if (waitMinutes > 10) return "High";
        }
        return "Normal";
    }

    public static ChatMessageDto ToDto(LiveChatMessage message) => new(
        message.Id,
        message.SessionId,
        message.SenderType,
        message.SenderName,
        message.SenderId,
        message.Body,
        message.SentAt);

    public static string CustomerDisplayName(LiveChatSession session) =>
        session.CustomerName ?? session.CustomerEmail ?? "Customer";
}

public class ListWaitingChatSessionsQueryHandler(
    IRepository<LiveChatSession> sessions,
    IIdentityUserService users,
    IMessageFactory messages)
    : IQueryHandler<ListWaitingChatSessionsQuery, Response<PaginatedList<ChatSessionDto>>>
{
    public async Task<Response<PaginatedList<ChatSessionDto>>> Handle(ListWaitingChatSessionsQuery request, CancellationToken ct)
    {
        var filter = PredicateBuilder.True<LiveChatSession>()
            .WhereIf(!string.IsNullOrWhiteSpace(request.Status), s => s.Status == request.Status!)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Search), s =>
                (s.CustomerName != null && s.CustomerName.Contains(request.Search!)) ||
                (s.CustomerEmail != null && s.CustomerEmail.Contains(request.Search!)));

        var pageIndex = Math.Max(request.PageIndex, 1);
        var pageSize = Math.Max(request.PageSize, 1);

        var total = await sessions.CountAsync(filter, ct);

        var allRows = await sessions.ListOrderedAsync(
            filter,
            s => s.CreatedAt,
            descending: false,
            ct);

        var agentIds = allRows
            .Where(s => s.ClaimedByAgentId.HasValue)
            .Select(s => s.ClaimedByAgentId!.Value)
            .Distinct()
            .ToList();

        var agentNames = new Dictionary<Guid, string>();
        foreach (var id in agentIds)
        {
            var agent = await users.FindByIdAsync(id, ct);
            if (agent != null)
            {
                agentNames[id] = agent.FullName;
            }
        }

        var sortedRows = allRows;

        if (!string.IsNullOrWhiteSpace(request.SortBy))
        {
            var descending = request.SortDirection?.ToLowerInvariant() == "desc";
            if (request.SortBy == "customerName")
            {
                sortedRows = descending
                    ? allRows.OrderByDescending(s => s.CustomerName ?? "").ToList()
                    : allRows.OrderBy(s => s.CustomerName ?? "").ToList();
            }
            else
            {
                sortedRows = descending
                    ? allRows.OrderByDescending(s => s.CreatedAt).ToList()
                    : allRows.OrderBy(s => s.CreatedAt).ToList();
            }
        }

        var pagedRows = sortedRows
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var items = pagedRows.Select(s => LiveChatMapping.ToDto(s, s.ClaimedByAgentId.HasValue && agentNames.TryGetValue(s.ClaimedByAgentId.Value, out var name) ? name : null)).ToList();

        return Response<PaginatedList<ChatSessionDto>>.Ok(
            PaginatedList<ChatSessionDto>.Create(items, total, pageIndex, pageSize),
            ApplicationErrors.General.SUCCESS_OPERATION, "OK");
    }
}

public class ClaimChatSessionCommandHandler(
    IRepository<LiveChatSession> sessions,
    IIdentityUserService users,
    IUserContext user,
    IUnitOfWork unitOfWork,
    IRealTimeNotifier realtime,
    IMessageFactory messages)
    : ICommandHandler<ClaimChatSessionCommand, Response<ChatSessionDto>>
{
    public async Task<Response<ChatSessionDto>> Handle(ClaimChatSessionCommand request, CancellationToken ct)
    {
        var session = await sessions.GetTrackedAsync(request.SessionId, ct);
        if (session is null)
        {
            return messages.NotFound<ChatSessionDto>(ApplicationErrors.General.RESOURCE_NOT_FOUND);
        }

        try
        {
            session.Claim(user.UserId);
        }
        catch (InvalidOperationException)
        {
            return messages.Fail<ChatSessionDto>(ApplicationErrors.Ticket.TRANSITION_NOT_ALLOWED, MessageType.Conflict);
        }

        await unitOfWork.SaveChangesAsync(ct);
        var agentName = (await users.FindByIdAsync(user.UserId, ct))?.FullName;
        return messages.Success(LiveChatMapping.ToDto(session, agentName), ApplicationErrors.General.SUCCESS_UPDATED);
    }
}

public class GetChatTranscriptQueryHandler(
    IRepository<LiveChatSession> sessions,
    IRepository<LiveChatMessage> chatMessages,
    IMessageFactory messages)
    : IQueryHandler<GetChatTranscriptQuery, Response<IReadOnlyList<ChatMessageDto>>>
{
    public async Task<Response<IReadOnlyList<ChatMessageDto>>> Handle(GetChatTranscriptQuery request, CancellationToken ct)
    {
        if (!await sessions.ExistsAsync(s => s.Id == request.SessionId, ct))
        {
            return messages.NotFound<IReadOnlyList<ChatMessageDto>>(ApplicationErrors.General.RESOURCE_NOT_FOUND);
        }

        var rows = await chatMessages.ListOrderedAsync(
            m => m.SessionId == request.SessionId,
            m => m.SentAt,
            descending: false,
            ct);

        return messages.Success<IReadOnlyList<ChatMessageDto>>(
            rows.Select(LiveChatMapping.ToDto).ToList(),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }
}

public class SendAgentChatMessageCommandHandler(
    IRepository<LiveChatSession> sessions,
    IRepository<LiveChatMessage> chatMessages,
    IIdentityUserService users,
    IUserContext user,
    IUnitOfWork unitOfWork,
    IMessagePublisher publisher,
    IMessageFactory messages)
    : ICommandHandler<SendAgentChatMessageCommand, Response<ChatMessageDto>>
{
    public async Task<Response<ChatMessageDto>> Handle(SendAgentChatMessageCommand request, CancellationToken ct)
    {
        var session = await sessions.GetTrackedAsync(request.SessionId, ct);
        if (session is null)
        {
            return messages.NotFound<ChatMessageDto>(ApplicationErrors.General.RESOURCE_NOT_FOUND);
        }

        if (session.Status != "Active")
        {
            return messages.Fail<ChatMessageDto>(ApplicationErrors.Ticket.TRANSITION_NOT_ALLOWED, MessageType.Conflict);
        }

        if (session.ClaimedByAgentId is not null && session.ClaimedByAgentId != user.UserId)
        {
            return messages.Fail<ChatMessageDto>(ApplicationErrors.General.FORBIDDEN, MessageType.Forbidden);
        }

        var agentName = (await users.FindByIdAsync(user.UserId, ct))?.FullName ?? user.Email ?? "Agent";
        var message = LiveChatMessage.Create(session.Id, "Agent", agentName, user.UserId, request.Body);
        await chatMessages.AddAsync(message, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await publisher.PublishAsync(
            Topics.ChatMessagesPushed,
            new ChatMessagePushed(
                message.Id,
                message.SessionId,
                message.SenderType,
                message.SenderName,
                message.SenderId,
                message.Body,
                message.SentAt),
            ct);

        return messages.Success(LiveChatMapping.ToDto(message), ApplicationErrors.Ticket.MESSAGE_RECORDED);
    }
}

public class CloseChatSessionCommandHandler(
    IRepository<LiveChatSession> sessions,
    IUserContext user,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<CloseChatSessionCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(CloseChatSessionCommand request, CancellationToken ct)
    {
        var session = await sessions.GetTrackedAsync(request.SessionId, ct);
        if (session is null)
        {
            return messages.NotFound<Unit>(ApplicationErrors.General.RESOURCE_NOT_FOUND);
        }

        try
        {
            session.Close(user.UserId);
        }
        catch (InvalidOperationException)
        {
            return messages.Fail<Unit>(ApplicationErrors.Ticket.TRANSITION_NOT_ALLOWED, MessageType.Conflict);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_UPDATED);
    }
}

public class StartAnonymousChatSessionCommandHandler(
    IRepository<LiveChatSession> sessions,
    IRepository<LiveChatMessage> chatMessages,
    IUnitOfWork unitOfWork,
    IRealTimeNotifier realtime,
    IMessageFactory messages)
    : ICommandHandler<StartAnonymousChatSessionCommand, Response<StartChatSessionResponse>>
{
    public async Task<Response<StartChatSessionResponse>> Handle(StartAnonymousChatSessionCommand request, CancellationToken ct)
    {
        var (session, token) = LiveChatSession.Start(request.CustomerName, request.CustomerEmail);
        await sessions.AddAsync(session, ct);

        if (!string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            await chatMessages.AddAsync(
                LiveChatMessage.Create(session.Id, "Customer", LiveChatMapping.CustomerDisplayName(session), null, request.InitialMessage),
                ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return messages.Success(new StartChatSessionResponse(token, session.Id), ApplicationErrors.General.SUCCESS_CREATED);
    }
}

public class SendAnonymousChatMessageCommandHandler(
    IRepository<LiveChatSession> sessions,
    IRepository<LiveChatMessage> chatMessages,
    IUnitOfWork unitOfWork,
    IMessagePublisher publisher,
    IMessageFactory messages)
    : ICommandHandler<SendAnonymousChatMessageCommand, Response<ChatMessageDto>>
{
    public async Task<Response<ChatMessageDto>> Handle(SendAnonymousChatMessageCommand request, CancellationToken ct)
    {
        LiveChatSession? session;
        try
        {
            var hash = LiveChatSession.HashToken(request.Token);
            var match = await sessions.FirstOrDefaultAsync(s => s.SessionTokenHash == hash, ct);
            session = match is null ? null : await sessions.GetTrackedAsync(match.Id, ct);
        }
        catch (ArgumentException)
        {
            session = null;
        }

        if (session is null)
        {
            return messages.NotFound<ChatMessageDto>(ApplicationErrors.General.RESOURCE_NOT_FOUND);
        }

        try
        {
            session.EnsureOpenForCustomer();
        }
        catch (InvalidOperationException)
        {
            return messages.Fail<ChatMessageDto>(ApplicationErrors.Ticket.TRANSITION_NOT_ALLOWED, MessageType.Conflict);
        }

        var message = LiveChatMessage.Create(session.Id, "Customer", LiveChatMapping.CustomerDisplayName(session), null, request.Body);
        await chatMessages.AddAsync(message, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await publisher.PublishAsync(
            Topics.ChatMessagesPushed,
            new ChatMessagePushed(
                message.Id,
                message.SessionId,
                message.SenderType,
                message.SenderName,
                message.SenderId,
                message.Body,
                message.SentAt),
            ct);

        return messages.Success(LiveChatMapping.ToDto(message), ApplicationErrors.Ticket.MESSAGE_RECORDED);
    }
}

public class GetAnonymousChatTranscriptQueryHandler(
    IRepository<LiveChatSession> sessions,
    IRepository<LiveChatMessage> chatMessages,
    IMessageFactory messages)
    : IQueryHandler<GetAnonymousChatTranscriptQuery, Response<IReadOnlyList<ChatMessageDto>>>
{
    public async Task<Response<IReadOnlyList<ChatMessageDto>>> Handle(GetAnonymousChatTranscriptQuery request, CancellationToken ct)
    {
        LiveChatSession? session;
        try
        {
            var hash = LiveChatSession.HashToken(request.Token);
            session = await sessions.FirstOrDefaultAsync(s => s.SessionTokenHash == hash, ct);
        }
        catch (ArgumentException)
        {
            session = null;
        }

        if (session is null)
        {
            return messages.NotFound<IReadOnlyList<ChatMessageDto>>(ApplicationErrors.General.RESOURCE_NOT_FOUND);
        }

        var rows = await chatMessages.ListOrderedAsync(
            m => m.SessionId == session.Id,
            m => m.SentAt,
            descending: false,
            ct);

        return messages.Success<IReadOnlyList<ChatMessageDto>>(
            rows.Select(LiveChatMapping.ToDto).ToList(),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }
}

public class SuggestChatReplyCommandHandler(
    IRepository<LiveChatSession> sessions,
    IRepository<LiveChatMessage> chatMessages,
    IAiService ai,
    IUserContext user,
    IMessageFactory messages)
    : ICommandHandler<SuggestChatReplyCommand, Response<ChatReplySuggestionDto>>
{
    public async Task<Response<ChatReplySuggestionDto>> Handle(SuggestChatReplyCommand request, CancellationToken ct)
    {
        if (!ai.IsAvailable)
        {
            return AiMapping.NotConfigured<ChatReplySuggestionDto>(messages);
        }

        var session = await sessions.GetByIdAsync(request.SessionId, ct);
        if (session is null)
        {
            return messages.NotFound<ChatReplySuggestionDto>(ApplicationErrors.General.RESOURCE_NOT_FOUND);
        }

        if (session.Status == "Active" && session.ClaimedByAgentId is not null && session.ClaimedByAgentId != user.UserId)
        {
            return messages.Fail<ChatReplySuggestionDto>(ApplicationErrors.General.FORBIDDEN, MessageType.Forbidden);
        }

        var transcript = await chatMessages.ListOrderedAsync(
            m => m.SessionId == request.SessionId,
            m => m.SentAt,
            descending: false,
            ct);

        if (transcript.Count == 0)
        {
            return messages.Fail<ChatReplySuggestionDto>(ApplicationErrors.General.AI_THREAD_TOO_SHORT, MessageType.Validation);
        }

        var threadText = string.Join("\n", transcript.Select(m => $"{m.SenderType} {m.SenderName}: {m.Body}"));
        var outcome = await ai.DraftReplyAsync(
            threadText,
            "Return three concise, empathetic live-chat reply drafts as a JSON string array. Do not promise actions the agent did not confirm.",
            ct);

        if (!outcome.Success)
        {
            return AiMapping.ProviderFailed<ChatReplySuggestionDto>(messages);
        }

        var drafts = (AiJson.ParseStringArray(outcome.Value) ?? [])
            .Where(draft => !string.IsNullOrWhiteSpace(draft))
            .Select(draft => draft.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();

        if (drafts.Count == 0)
        {
            return AiMapping.ProviderFailed<ChatReplySuggestionDto>(messages);
        }

        var customerCount = transcript.Count(m => m.SenderType == "Customer");
        var agentCount = transcript.Count(m => m.SenderType == "Agent");
        var latestCustomer = transcript.LastOrDefault(m => m.SenderType == "Customer")?.Body ?? "No customer message yet.";
        var summary = $"{customerCount} customer message(s), {agentCount} agent reply/replies. Latest customer note: {latestCustomer}";

        return messages.Success(new ChatReplySuggestionDto(drafts, summary), "AI_DRAFT_READY");
    }
}
