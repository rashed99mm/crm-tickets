using System.Text.Json;
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Tickets.Commands.CreateTicket;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Ai;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.Ai.Chat;

/// <summary>One rendered turn of a conversation.</summary>
public sealed record AiChatTurnDto(
    Guid Id, string Role, string Body, IReadOnlyList<KbCitationDto> Citations);

/// <summary>A conversation as the UI renders it: identity, state, and the turns in order.</summary>
public sealed record AiChatDto(
    Guid SessionId, string Status, Guid? TicketId, IReadOnlyList<AiChatTurnDto> Turns);

/// <summary>
/// AI-38 — opens a session and immediately answers the first message. The scope comes from the
/// host controller (staff vs portal), never from the client, so a portal caller cannot mint a
/// staff session or the reverse.
/// </summary>
public record StartAiChatCommand(string Message, AiChatScope Scope)
    : IRequest<Response<AiChatDto>>;

/// <summary>AI-39 — appends a turn to an open session owned by the caller.</summary>
public record SendAiChatMessageCommand(Guid SessionId, string Message, AiChatScope Scope)
    : IRequest<Response<AiChatDto>>;

/// <summary>Loads a session's transcript. Ownership and scope are checked (AI-40).</summary>
public record GetAiChatQuery(Guid SessionId, AiChatScope Scope)
    : IRequest<Response<AiChatDto>>;

/// <summary>
/// AI-42/AI-46 — closes a conversation by creating a ticket from its transcript through the
/// existing customer ticket path. Staff handoff names the customer and category explicitly; a
/// portal handoff resolves the customer by the portal account's email. <c>Scope</c> arrives from
/// the host, never the client.
/// </summary>
public record HandoffFromChatCommand(
    Guid SessionId, Guid? CustomerId, Guid? CategoryId, AiChatScope Scope)
    : IRequest<Response<Guid>>;

public record StartAiChatRequest(string Message);
public record SendAiChatMessageRequest(string Message);
public record HandoffFromChatRequest(Guid? CustomerId, Guid? CategoryId);

/// <summary>Shared engine for every chat command: authorization, retrieval, grounded answer, persistence.</summary>
public class AiChatService(
    IRepository<AiChatSession> sessions,
    IRepository<AiChatMessage> turns,
    IRepository<Content> contents,
    IAiService ai,
    IUnitOfWork uow,
    IMessageFactory factory)
{
    private const int MaxHistoryTurns = 20;

    public bool Available => ai.IsAvailable;

    public async Task<Response<AiChatSession>> LoadOwnedAsync(
        Guid sessionId, Guid actorId, AiChatScope scope, CancellationToken ct)
    {
        var session = await sessions.FirstOrDefaultAsync(
            s => s.Id == sessionId && !s.IsDeleted, ct);

        // AI-40 — another actor's, another scope's, and an unknown id are the same safe not-found.
        if (session is null || !session.BelongsTo(actorId, scope))
        {
            return factory.Fail<AiChatSession>(ApplicationErrors.General.AI_CHAT_NOT_FOUND, MessageType.NotFound);
        }

        return factory.Success(session, ApplicationErrors.General.SUCCESS_OPERATION);
    }

    public async Task<Response<AiChatDto>> AnswerAsync(AiChatSession session, string message, CancellationToken ct)
    {
        if (session.Status == AiChatStatus.Closed)
        {
            return factory.Fail<AiChatDto>("TICKET_TRANSITION_NOT_ALLOWED", MessageType.Conflict);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return factory.Fail<AiChatDto>(ApplicationErrors.General.BAD_REQUEST, MessageType.Validation);
        }

        await turns.AddAsync(AiChatMessage.UserTurn(session.Id, message), ct);

        // AI-39 — history informs the question: prior user turns are folded in as context so a
        // follow-up like "and what about billing?" still retrieves the right articles.
        var history = (await turns.ListProjectedOrderedAsync(
                t => t.SessionId == session.Id,
                t => new { t.Role, t.Body },
                t => t.CreatedAtUtc, true, ct))
            .Take(MaxHistoryTurns)
            .Reverse()
            .ToList();

        var conversationContext = string.Join("\n", history.Select(t =>
            (t.Role == AiChatRole.User ? "Customer: " : "Assistant: ") + t.Body));

        var published = await contents.ListProjectedAsync(
            c => c.Status == "Published",
            c => new KbPassage(c.Id, c.Title, c.Body), ct);
        var ranked = KbRetriever.Retrieve(message, published, 5).ToList();

        var outcome = await ai.AnswerAsync(conversationContext, ranked, ct);
        if (!outcome.Success)
        {
            return factory.Fail<AiChatDto>(ApplicationErrors.General.AI_PROVIDER_FAILED, MessageType.Internal);
        }

        var ungrounded = ranked.Count == 0 || outcome.Value!.Contains(AiContract.UngroundedSentinel);
        var answerBody = ungrounded
            ? "I could not find this in the knowledge base. Would you like me to hand this to a human agent?"
            : outcome.Value!;

        var citations = ungrounded
            ? []
            : ranked.Where(p => outcome.Value!.Contains(p.Title, StringComparison.OrdinalIgnoreCase))
                .Select(p => new KbCitationDto(p.ArticleId, p.Title))
                .Take(3)
                .ToList();

        await turns.AddAsync(
            AiChatMessage.AssistantTurn(session.Id, answerBody, JsonSerializer.Serialize(citations)), ct);

        // AI-38/39 — both turns (and the session row, for a new conversation) are only flushed once
        // the assistant answer was generated successfully. A provider failure leaves nothing on disk:
        // the caller sees the error and retries, which creates a fresh attempt rather than an orphaned
        // half-turn that a later GET would surface as a stuck conversation.
        await uow.SaveChangesAsync(ct);

        return factory.Success(await ToDtoAsync(session, turns, ct), "AI_ANSWER_READY");
    }

    public static async Task<AiChatDto> ToDtoAsync(
        AiChatSession session, IRepository<AiChatMessage> turnRepository, CancellationToken ct)
    {
        var history = await turnRepository.ListProjectedOrderedAsync(
                t => t.SessionId == session.Id,
                t => new { t.Id, Role = (int)t.Role, t.Body, t.CitationsJson },
                t => t.CreatedAtUtc, false, ct);

        var turns = history.Select(t => new AiChatTurnDto(
            t.Id,
            t.Role == (int)AiChatRole.Assistant ? "assistant" : "user",
            t.Body,
            SafeCitations(t.CitationsJson))).ToList();

        return new AiChatDto(
            session.Id,
            session.Status == AiChatStatus.Closed ? "Closed" : "Open",
            session.TicketId,
            turns);
    }

    private static IReadOnlyList<KbCitationDto> SafeCitations(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<KbCitationDto>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public class StartAiChatCommandHandler(
    IRepository<AiChatSession> sessions,
    AiChatService chat,
    IUserContext user,
    IMessageFactory factory)
    : IRequestHandler<StartAiChatCommand, Response<AiChatDto>>
{
    public async Task<Response<AiChatDto>> Handle(StartAiChatCommand request, CancellationToken ct)
    {
        if (!chat.Available)
        {
            return AiMapping.NotConfigured<AiChatDto>(factory);
        }

        if (!user.IsAuthenticated || user.UserId == Guid.Empty)
        {
            return factory.Fail<AiChatDto>(ApplicationErrors.Auth.NOT_AUTHENTICATED, MessageType.Unauthorized);
        }

        var session = AiChatSession.Create(user.UserId, request.Scope);
        await sessions.AddAsync(session, ct);

        var created = await chat.AnswerAsync(session, request.Message, ct);
        if (!created.Success)
        {
            return created;
        }

        return factory.Success(created.Data!, "AI_ANSWER_READY");
    }
}

public class SendAiChatMessageCommandHandler(
    AiChatService chat, IUserContext user, IMessageFactory factory)
    : IRequestHandler<SendAiChatMessageCommand, Response<AiChatDto>>
{
    public async Task<Response<AiChatDto>> Handle(SendAiChatMessageCommand request, CancellationToken ct)
    {
        if (!chat.Available)
        {
            return AiMapping.NotConfigured<AiChatDto>(factory);
        }

        var owned = await chat.LoadOwnedAsync(request.SessionId, user.UserId, request.Scope, ct);
        if (!owned.Success)
        {
            return Response<AiChatDto>.Fail(owned.Code, owned.Message, MessageType.NotFound);
        }

        return await chat.AnswerAsync(owned.Data!, request.Message, ct);
    }
}

public class GetAiChatQueryHandler(
    IRepository<AiChatSession> sessions,
    IRepository<AiChatMessage> turns,
    IUserContext user,
    IMessageFactory factory)
    : IRequestHandler<GetAiChatQuery, Response<AiChatDto>>
{
    public async Task<Response<AiChatDto>> Handle(GetAiChatQuery request, CancellationToken ct)
    {
        var session = await sessions.FirstOrDefaultAsync(
            s => s.Id == request.SessionId && !s.IsDeleted, ct);

        if (session is null || !session.BelongsTo(user.UserId, request.Scope))
        {
            return factory.Fail<AiChatDto>(ApplicationErrors.General.AI_CHAT_NOT_FOUND, MessageType.NotFound);
        }

        return factory.Success(await AiChatService.ToDtoAsync(session, turns, ct), ApplicationErrors.General.SUCCESS_OPERATION);
    }
}

public class HandoffFromChatCommandHandler(
    IRepository<AiChatSession> sessions,
    IRepository<AiChatMessage> turns,
    IRepository<Customer> customers,
    IRepository<Category> categories,
    IUnitOfWork uow,
    AiChatService chat,
    IMediator mediator,
    IUserContext user,
    IMessageFactory factory)
    : IRequestHandler<HandoffFromChatCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(HandoffFromChatCommand request, CancellationToken ct)
    {
        var owned = await chat.LoadOwnedAsync(request.SessionId, user.UserId, request.Scope, ct);
        if (!owned.Success)
        {
            return Response<Guid>.Fail(owned.Code, owned.Message, MessageType.NotFound);
        }

        var session = owned.Data!;
        var (customerId, categoryFailure) = await ResolveCustomerAsync(session, request.CustomerId, ct);
        if (categoryFailure is not null)
        {
            return categoryFailure;
        }

        var categoryId = request.CategoryId
            ?? (await categories.ListAsync(c => c.IsActive, ct)).FirstOrDefault()?.Id;
        if (categoryId is null)
        {
            return factory.Fail<Guid>(ApplicationErrors.General.BAD_REQUEST, MessageType.Validation);
        }

        // AI-46 — the transcript becomes the ticket description through the standard capture path.
        var transcript = await BuildTranscriptAsync(session.Id, ct);
        var created = await mediator.Send(new CreateTicketCommand(
            Subject: "Handed over from AI assistant",
            Description: transcript,
            CustomerId: customerId!.Value,
            CategoryId: categoryId.Value,
            Impact: "Medium",
            Urgency: "Medium"), ct);

        if (!created.Success)
        {
            return Response<Guid>.Fail(created.Code, created.Message, MessageType.Conflict);
        }

        session.AttachTicket(created.Data!);
        await uow.SaveChangesAsync(ct);

        return factory.Success(created.Data!, "TICKET_CREATED");
    }

    private async Task<(Guid? CustomerId, Response<Guid>? Failure)> ResolveCustomerAsync(
        AiChatSession session, Guid? explicitCustomerId, CancellationToken ct)
    {
        if (session.Scope == AiChatScope.Staff)
        {
            return (explicitCustomerId,
                explicitCustomerId is null
                    ? factory.Fail<Guid>(ApplicationErrors.General.BAD_REQUEST, MessageType.Validation)
                    : null);
        }

        // Portal handoff: the portal account's email identifies the customer record.
        var email = user.Email;
        var customer = (await customers.ListAsync(c => c.Email == email, ct)).FirstOrDefault();
        return customer is null
            ? (null, factory.Fail<Guid>(ApplicationErrors.General.BAD_REQUEST, MessageType.Validation))
            : (customer.Id, null);
    }

    private async Task<string> BuildTranscriptAsync(Guid sessionId, CancellationToken ct)
    {
        var history = await turns.ListProjectedOrderedAsync(
            t => t.SessionId == sessionId,
            t => new { t.Role, t.Body },
            t => t.CreatedAtUtc, false, ct);

        return string.Join("\n", history.Select(t =>
            (t.Role == AiChatRole.User ? "Customer: " : "Assistant: ") + t.Body));
    }
}


