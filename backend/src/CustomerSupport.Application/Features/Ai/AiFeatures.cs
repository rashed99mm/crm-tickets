using System.Text.Json;
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Ai;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;
using MediatR;

namespace CustomerSupport.Application.Features.Ai;

/// <summary>
/// FEAT-21 — the five AI drafting features and the QA ask behaviour, plus the suggestion
/// lifecycle commands of US-708.
///
/// Invariants shared by every feature here:
/// - Degraded mode first: with no provider configured, the documented ERR052 envelope is returned
///   and nothing is read or written (spec A2).
/// - The human gate: generation only *stores* a Pending suggestion; nothing here mutates a ticket
///   except accepting a category suggestion, which goes through the ticket entity itself.
/// - Authorization mirrors AC-43/45: supervisor-any, agent-own.
/// </summary>

public sealed record AiSuggestionDto(Guid Id, string Kind, JsonElement Payload, string Status, bool Edited);

internal static class AiMapping
{
    public static AiSuggestionDto ToDto(AiSuggestion s) => new(
        s.Id, s.Kind, JsonSerializer.Deserialize<JsonElement>(s.Payload), s.Status, s.Edited);

    public static Response<T> NotConfigured<T>(IMessageFactory messages) =>
        messages.Fail<T>(ApplicationErrors.General.AI_NOT_CONFIGURED, MessageType.BusinessRule);

    /// <summary>AI-32 — the provider chain exhausted every configured provider. Surfaces the
    /// dedicated 503 code instead of a generic internal error, so the client can retry later.</summary>
    public static Response<T> ProviderFailed<T>(IMessageFactory messages) =>
        messages.Fail<T>(ApplicationErrors.General.AI_PROVIDER_FAILED, MessageType.Internal);

    /// <summary>AC-43/AC-45's ownership rule applied to every AI surface.</summary>
    public static async Task<Response<Unit>?> AuthorizeTicketAsync(
        IRepository<Ticket> tickets, Guid ticketId, IUserContext user, IMessageFactory messages)
    {
        var ticket = await tickets.GetByIdAsync(ticketId);
        if (ticket is null)
        {
            return messages.NotFound<Unit>("TICKET_NOT_FOUND");
        }

        if (!user.HasAnyRole("Supervisor", "Admin") && ticket.AssigneeId != user.UserId)
        {
            return messages.Fail<Unit>(ApplicationErrors.General.FORBIDDEN, MessageType.Forbidden);
        }

        return null;
    }
}

// ── US-704 · Summarise ───────────────────────────────────────────────────────────────────────

public record SummariseTicketCommand(Guid TicketId) : IRequest<Response<AiSuggestionDto>>;

public class SummariseTicketCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketMessage> messages,
    IRepository<AiSuggestion> suggestions,
    IAiService ai,
    IUserContext user,
    IUnitOfWork uow,
    IMessageFactory factory) : IRequestHandler<SummariseTicketCommand, Response<AiSuggestionDto>>
{
    private const int MinimumThreadMessages = 2;

    public async Task<Response<AiSuggestionDto>> Handle(SummariseTicketCommand request, CancellationToken ct)
    {
        if (!ai.IsAvailable)
        {
            return AiMapping.NotConfigured<AiSuggestionDto>(factory);
        }

        if (await AiMapping.AuthorizeTicketAsync(tickets, request.TicketId, user, factory) is { } denied)
        {
            return ToDto<AiSuggestionDto>(denied, factory);
        }

        var thread = await messages.ListProjectedAsync(
            m => m.TicketId == request.TicketId,
            m => new { m.Body });
        if (thread.Count < MinimumThreadMessages)
        {
            // US-704 AC3 — a two-line thread has nothing to compress; saying so beats a "summary"
            // that just repeats the description back.
            return factory.Fail<AiSuggestionDto>(
                ApplicationErrors.General.AI_THREAD_TOO_SHORT, MessageType.Validation);
        }

        var threadText = string.Join("\n", thread.Select(m => "- " + m.Body));

        var summaryOutcome = await ai.SummariseAsync(threadText, ct);
        if (!summaryOutcome.Success)
        {
            return AiMapping.ProviderFailed<AiSuggestionDto>(factory);
        }

        // AC-21.11 / A5 — a sentiment failure never fails the summary. The handler treats a
        // null/garbage label as "no sentiment" and continues, so the only error the agent ever
        // sees is on the actual summary call.
        string? sentiment = null;
        var sentimentOutcome = await ai.ClassifySentimentAsync(threadText, ct);
        if (sentimentOutcome.Success && sentimentOutcome.Value is { } raw)
        {
            sentiment = raw is "Frustrated" or "Neutral" or "Satisfied" ? raw : null;
        }

        var payload = JsonSerializer.Serialize(new
        {
            text = summaryOutcome.Value,
            sentiment,
        });
        var suggestion = AiSuggestion.Create(
            request.TicketId, "Summary", payload, user.UserId);
        await suggestions.AddAsync(suggestion, ct);
        await uow.SaveChangesAsync(ct);

        return factory.Success(AiMapping.ToDto(suggestion), "AI_SUMMARY_READY");
    }

    private static Response<T> ToDto<T>(Response<Unit> denied, IMessageFactory factory) =>
        factory.Fail<T>(denied.Code switch
        {
            SystemCode.ERR001 => "TICKET_NOT_FOUND",
            _ => ApplicationErrors.General.FORBIDDEN,
        }, MessageType.Forbidden);
}

// ── US-705 · Category suggestion ─────────────────────────────────────────────────────────────

public record SuggestCategoriesCommand(Guid TicketId) : IRequest<Response<AiSuggestionDto>>;

public class SuggestCategoriesCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<Category> categories,
    IRepository<AiSuggestion> suggestions,
    IAiService ai,
    IUserContext user,
    IUnitOfWork uow,
    IMessageFactory factory) : IRequestHandler<SuggestCategoriesCommand, Response<AiSuggestionDto>>
{
    public async Task<Response<AiSuggestionDto>> Handle(SuggestCategoriesCommand request, CancellationToken ct)
    {
        if (!ai.IsAvailable)
        {
            return AiMapping.NotConfigured<AiSuggestionDto>(factory);
        }

        if (await AiMapping.AuthorizeTicketAsync(tickets, request.TicketId, user, factory) is { } denied)
        {
            return Denied<AiSuggestionDto>(denied);
        }

        var names = (await categories.ListAsync(c => c.IsActive, ct))
            .Select(c => c.Name).ToList();
        if (names.Count == 0)
        {
            return factory.Fail<AiSuggestionDto>(ApplicationErrors.General.INTERNAL_ERROR, MessageType.Internal);
        }

        var ticket = await tickets.GetByIdAsync(request.TicketId, ct);
        var outcome = await ai.SuggestCategoriesAsync(
            $"{ticket!.Subject}\n{ticket.Description}", names, ct);
        if (!outcome.Success)
        {
            return AiMapping.ProviderFailed<AiSuggestionDto>(factory);
        }

        var payload = JsonSerializer.Serialize(new
        {
            options = names
                .Where(n => outcome.Value!.Contains(n, StringComparer.OrdinalIgnoreCase))
                .Select(n => new { name = n })
                .ToList(),
        });

        var suggestion = AiSuggestion.Create(request.TicketId, "Categories", payload, user.UserId);
        await suggestions.AddAsync(suggestion, ct);
        await uow.SaveChangesAsync(ct);
        return factory.Success(AiMapping.ToDto(suggestion), "AI_CATEGORIES_READY");
    }

    private static Response<T> Denied<T>(Response<Unit> denied) =>
        Response<T>.Fail(denied.Code, denied.Message,
            denied.Code == SystemCode.ERR001 ? MessageType.NotFound : MessageType.Forbidden);
}

// ── US-706 · Draft reply ─────────────────────────────────────────────────────────────────────

public record DraftReplyCommand(Guid TicketId, string? Instruction = null)
    : IRequest<Response<AiSuggestionDto>>;

public record DraftReplyRequest(string? Instruction);

public class DraftReplyCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketMessage> messages,
    IRepository<AiSuggestion> suggestions,
    IAiService ai,
    IUserContext user,
    IUnitOfWork uow,
    IMessageFactory factory) : IRequestHandler<DraftReplyCommand, Response<AiSuggestionDto>>
{
    public async Task<Response<AiSuggestionDto>> Handle(DraftReplyCommand request, CancellationToken ct)
    {
        if (!ai.IsAvailable)
        {
            return AiMapping.NotConfigured<AiSuggestionDto>(factory);
        }

        if (await AiMapping.AuthorizeTicketAsync(tickets, request.TicketId, user, factory) is { } denied)
        {
            return Denied<AiSuggestionDto>(denied);
        }

        var ticket = await tickets.GetByIdAsync(request.TicketId, ct);
        var thread = await messages.ListProjectedAsync(
            m => m.TicketId == request.TicketId, m => new { m.Body }, ct);

        var threadText = $"{ticket!.Subject}\n{ticket.Description}\n" +
                         string.Join("\n", thread.Select(m => "- " + m.Body));

        var outcome = await ai.DraftReplyAsync(threadText, request.Instruction, ct);
        if (!outcome.Success)
        {
            return AiMapping.ProviderFailed<AiSuggestionDto>(factory);
        }

        // AC-21.12 — the provider's prompt asks for three drafts in one call, returned as a
        // schema-told JSON document. Parse strictly (AI-36): malformed output is a safe failure,
        // never a draft of "". De-duplicate and cap to 3.
        var drafts = (AiJson.ParseStringArray(outcome.Value) ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();
        if (drafts.Count == 0)
        {
            return AiMapping.ProviderFailed<AiSuggestionDto>(factory);
        }

        var payload = JsonSerializer.Serialize(new { drafts });
        var suggestion = AiSuggestion.Create(request.TicketId, "Reply", payload, user.UserId);
        await suggestions.AddAsync(suggestion, ct);
        await uow.SaveChangesAsync(ct);
        return factory.Success(AiMapping.ToDto(suggestion), "AI_DRAFT_READY");
    }

    private static Response<T> Denied<T>(Response<Unit> denied) =>
        Response<T>.Fail(denied.Code, denied.Message,
            denied.Code == SystemCode.ERR001 ? MessageType.NotFound : MessageType.Forbidden);
}

// ── US-707 · Suggested solutions from the published KB ──────────────────────────────────────

public record SuggestSolutionsCommand(Guid TicketId) : IRequest<Response<AiSuggestionDto>>;

public class SuggestSolutionsCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<Content> contents,
    IRepository<AiSuggestion> suggestions,
    IAiService ai,
    IUserContext user,
    IUnitOfWork uow,
    IMessageFactory factory) : IRequestHandler<SuggestSolutionsCommand, Response<AiSuggestionDto>>
{
    public async Task<Response<AiSuggestionDto>> Handle(SuggestSolutionsCommand request, CancellationToken ct)
    {
        if (!ai.IsAvailable)
        {
            return AiMapping.NotConfigured<AiSuggestionDto>(factory);
        }

        if (await AiMapping.AuthorizeTicketAsync(tickets, request.TicketId, user, factory) is { } denied)
        {
            return Denied<AiSuggestionDto>(denied);
        }

        var ticket = await tickets.GetByIdAsync(request.TicketId, ct);

        // AC-707 — candidates are published articles only; drafts never reach the model or the UI.
        var published = await contents.ListProjectedAsync(
            c => c.Status == ContentStatus.Published.Value,
            c => new KbPassage(c.Id, c.Title, c.Body), ct);
        var candidates = published.Take(20).ToList();

        var outcome = await ai.SuggestSolutionsAsync($"{ticket!.Subject}: {ticket.Description}", candidates, ct);
        if (!outcome.Success)
        {
            return AiMapping.ProviderFailed<AiSuggestionDto>(factory);
        }

        var payload = JsonSerializer.Serialize(new
        {
            articles = outcome.Value!.Select(a => new { id = a.ArticleId, title = a.Title }).ToList(),
        });

        var suggestion = AiSuggestion.Create(request.TicketId, "Solutions", payload, user.UserId);
        await suggestions.AddAsync(suggestion, ct);
        await uow.SaveChangesAsync(ct);
        return factory.Success(AiMapping.ToDto(suggestion), "AI_SOLUTIONS_READY");
    }

    private static Response<T> Denied<T>(Response<Unit> denied) =>
        Response<T>.Fail(denied.Code, denied.Message,
            denied.Code == SystemCode.ERR001 ? MessageType.NotFound : MessageType.Forbidden);
}

// ── US-708 · Resolve and list suggestions ────────────────────────────────────────────────────

public record ResolveAiSuggestionCommand(
    Guid TicketId, Guid SuggestionId, string Action, string? EditedPayload = null)
    : IRequest<Response<AiSuggestionDto>>;

public record ResolveAiSuggestionRequest(string Action, string? EditedPayload);

/// <summary>
/// AC-703 — accept or reject is the only way a suggestion leaves Pending, and it is an explicit
/// agent decision. Accepting a Categories suggestion additionally applies it to the ticket through
/// the entity's own state method, so no category invariant can be bypassed from here.
/// </summary>
public class ResolveAiSuggestionCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<Category> categories,
    IRepository<AiSuggestion> suggestions,
    IAiService ai,
    IUserContext user,
    IUnitOfWork uow,
    IMessageFactory factory) : IRequestHandler<ResolveAiSuggestionCommand, Response<AiSuggestionDto>>
{
    public async Task<Response<AiSuggestionDto>> Handle(ResolveAiSuggestionCommand request, CancellationToken ct)
    {
        if (!ai.IsAvailable)
        {
            return AiMapping.NotConfigured<AiSuggestionDto>(factory);
        }

        if (request.Action is not ("accept" or "reject"))
        {
            return factory.Fail<AiSuggestionDto>("TICKET_TRANSITION_NOT_ALLOWED", MessageType.Validation);
        }

        if (await AiMapping.AuthorizeTicketAsync(tickets, request.TicketId, user, factory) is { } denied)
        {
            return Denied<AiSuggestionDto>(denied);
        }

        var suggestion = await suggestions.FirstOrDefaultAsync(
            s => s.Id == request.SuggestionId && s.TicketId == request.TicketId, ct);
        if (suggestion is null)
        {
            return factory.NotFound<AiSuggestionDto>("TICKET_NOT_FOUND");
        }

        if (!suggestion.Resolve(request.Action == "accept" ? "Accepted" : "Rejected", request.EditedPayload))
        {
            // US-708 AC1 — Accepted→Pending and double-resolve are refused, not idempotent.
            return factory.Fail<AiSuggestionDto>("TICKET_TRANSITION_NOT_ALLOWED", MessageType.Conflict);
        }

        if (request.Action == "accept" && suggestion.Kind == "Categories")
        {
            await ApplyCategoryAsync(suggestion, ct);
        }

        await uow.SaveChangesAsync(ct);
        return factory.Success(AiMapping.ToDto(suggestion), "SUCCESS_UPDATED");
    }

    private async Task ApplyCategoryAsync(AiSuggestion suggestion, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<JsonElement>(suggestion.Payload);
        if (payload.TryGetProperty("options", out var options) && options.GetArrayLength() > 0)
        {
            var name = options[0].GetProperty("name").GetString();
            var category = (await categories.ListAsync(c => c.Name == name, ct)).FirstOrDefault();
            var ticket = await tickets.GetTrackedAsync(suggestion.TicketId, ct);
            if (category is not null && ticket is not null)
            {
                ticket.ApplySuggestedCategory(category.Id);
            }
        }
    }

    private static Response<T> Denied<T>(Response<Unit> denied) =>
        Response<T>.Fail(denied.Code, denied.Message,
            denied.Code == SystemCode.ERR001 ? MessageType.NotFound : MessageType.Forbidden);
}

public record ListAiSuggestionsQuery(Guid TicketId) : IRequest<Response<List<AiSuggestionDto>>>;

public class ListAiSuggestionsQueryHandler(
    IRepository<Ticket> tickets,
    IRepository<AiSuggestion> suggestions,
    IAiService ai,
    IUserContext user,
    IMessageFactory factory) : IRequestHandler<ListAiSuggestionsQuery, Response<List<AiSuggestionDto>>>
{
    public async Task<Response<List<AiSuggestionDto>>> Handle(ListAiSuggestionsQuery request, CancellationToken ct)
    {
        if (!ai.IsAvailable)
        {
            return factory.Success<List<AiSuggestionDto>>([], "SUCCESS_OPERATION");
        }

        if (await AiMapping.AuthorizeTicketAsync(tickets, request.TicketId, user, factory) is { } denied)
        {
            return Response<List<AiSuggestionDto>>.Fail(
                denied.Code,
                denied.Message,
                denied.Code == SystemCode.ERR001 ? MessageType.NotFound : MessageType.Forbidden);
        }

        var rows = await suggestions.ListProjectedOrderedAsync(
            s => s.TicketId == request.TicketId && !s.IsDeleted,
            s => new AiSuggestionDto(s.Id, s.Kind,
                JsonSerializer.Deserialize<JsonElement>(s.Payload), s.Status, s.Edited),
            s => s.CreatedAtUtc, false, ct);

        return factory.Success(rows.ToList(), "SUCCESS_OPERATION");
    }
}

// ── QA chatbot ───────────────────────────────────────────────────────────────────────────────

public record AskKnowledgeBaseCommand(string Question) : IRequest<Response<AiAnswerDto>>;

public record AiAnswerDto(string Answer, IReadOnlyList<KbCitationDto> Citations);

public record KbCitationDto(Guid ArticleId, string Title);

/// <summary>
/// AC-21.9 / A4 — retrieval before generation: top-k published passages are placed in context and
/// the model may only compose from them. An empty retrieval or the NOT_IN_KB sentinel both answer
/// the documented ungrounded refusal rather than risking an invented answer.
/// </summary>
public class AskKnowledgeBaseCommandHandler(
    IRepository<Content> contents,
    IAiService ai,
    IMessageFactory factory) : IRequestHandler<AskKnowledgeBaseCommand, Response<AiAnswerDto>>
{
    private const int TopK = 5;
    private const int MinQuestionLength = 8;

    public async Task<Response<AiAnswerDto>> Handle(AskKnowledgeBaseCommand request, CancellationToken ct)
    {
        if (!ai.IsAvailable)
        {
            return AiMapping.NotConfigured<AiAnswerDto>(factory);
        }

        var question = request.Question.Trim();
        if (question.Length < MinQuestionLength)
        {
            return factory.Fail<AiAnswerDto>(ApplicationErrors.General.BAD_REQUEST, MessageType.Validation);
        }

        // AI-35 — BM25-style bilingual retrieval replaces the keyword-contains scan, so Arabic
        // queries and word variants actually reach the right article.
        var published = await contents.ListProjectedAsync(
            c => c.Status == ContentStatus.Published.Value,
            c => new KbPassage(c.Id, c.Title, c.Body), ct);
        var ranked = KbRetriever.Retrieve(question, published, TopK).ToList();

        var outcome = await ai.AnswerAsync(question, ranked, ct);
        if (!outcome.Success)
        {
            return AiMapping.ProviderFailed<AiAnswerDto>(factory);
        }

        if (ranked.Count == 0 || outcome.Value!.Contains(AiContract.UngroundedSentinel))
        {
            // A4 — refusing beats inventing; QA001 tells the client to render "ask a human".
            return factory.Fail<AiAnswerDto>(ApplicationErrors.General.AI_UNGROUNDED, MessageType.NotFound);
        }

        var citations = ranked
            .Where(p => outcome.Value.Contains(p.Title, StringComparison.OrdinalIgnoreCase))
            .Select(p => new KbCitationDto(p.ArticleId, p.Title))
            .ToList();

        return factory.Success(
            new AiAnswerDto(outcome.Value, citations.Take(3).ToList()), "AI_ANSWER_READY");
    }
}

