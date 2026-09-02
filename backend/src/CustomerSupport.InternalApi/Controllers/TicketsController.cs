using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Tickets.Commands.AddTicketAttachment;
using CustomerSupport.Application.Features.Tickets.Commands.AddTicketTag;
using CustomerSupport.Application.Features.Tickets.Commands.RemoveTicketTag;
using CustomerSupport.Application.Features.Tickets.Commands.AddTicketLink;
using CustomerSupport.Application.Features.Tickets.Commands.RemoveTicketLink;
using CustomerSupport.Application.Features.Tickets.Commands.AssignTicket;
using CustomerSupport.Application.Features.Tickets.Commands.ChangeTicketStatus;
using CustomerSupport.Application.Features.Tickets.Commands.CreateTicket;
using CustomerSupport.Application.Features.Tickets.Commands.ReclassifyTicket;
using CustomerSupport.Application.Features.Tickets.Dtos;
using CustomerSupport.Application.Features.Tickets.Commands.RecordTicketMessage;
using CustomerSupport.Application.Features.Tickets.Commands.TakeEscalation;
using CustomerSupport.Application.Features.Tickets.Queries.DownloadTicketAttachment;
using CustomerSupport.Application.Features.Tickets.Queries.GetAssignableAgents;
using CustomerSupport.Application.Features.Tickets.Queries.GetTicketAttachments;
using CustomerSupport.Application.Features.Tickets.Queries.GetTicketById;
using CustomerSupport.Application.Features.Tickets.Queries.GetTicketMessages;
using CustomerSupport.Application.Features.Tickets.Queries.GetTickets;
using CustomerSupport.Application.Features.Contents.Commands.LinkContentToTicket;
using CustomerSupport.Application.Features.Contents.Commands.UnlinkContentFromTicket;
using CustomerSupport.Application.Features.Contents.Queries.GetLinkedContent;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Tracked customer requests. FEAT-04 (capture) and FEAT-05 (queue).
/// </summary>
/// <remarks>
/// Assignment and status changes are deliberately absent: they belong to FEAT-06 and FEAT-07, and
/// exposing them here would be an endpoint satisfying no criterion in these features.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class TicketsController(IMediator mediator) : ControllerBase
{
    /// <summary>Lists tickets, newest first, with combinable filters.</summary>
    /// <remarks>
    /// Filters compose: <c>status</c> and <c>priority</c> together narrow to the intersection
    /// (AC-33). An unknown <c>status</c> or <c>priority</c> value is a 400 rather than an empty
    /// page, because an empty page would read as "nothing in that state".
    /// </remarks>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page; above the maximum this is a 400.</param>
    /// <param name="status">New, Open, Assigned, In Progress, Waiting for Customer, Waiting for Internal Team, Resolved or Closed.</param>
    /// <param name="priority">Low, Normal, High or Urgent.</param>
    /// <param name="customerId">Only tickets raised for this customer.</param>
    /// <param name="assigneeId">Only tickets held by this user. Ignored when <paramref name="mine"/> is set.</param>
    /// <param name="mine">Only the caller's own tickets, resolved from the token (AC-34).</param>
    /// <param name="unassigned">
    /// Only tickets nobody holds (AC-82). Distinct from omitting <paramref name="assigneeId"/>,
    /// which means "any assignee". Ignored when <paramref name="mine"/> is set.
    /// </param>
    /// <param name="tag">Only tickets carrying this tag, normalized before matching (US-924, AC-924.4).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(Response<PaginatedList<TicketListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<TicketListItemDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? assigneeId = null,
        [FromQuery] bool mine = false,
        [FromQuery] bool unassigned = false,
        [FromQuery] string? tag = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetTicketsQuery
            {
                PageIndex = page,
                PageSize = pageSize,
                Status = status,
                Priority = priority,
                CustomerId = customerId,
                AssigneeId = assigneeId,
                Mine = mine,
                Unassigned = unassigned,
                Tag = tag,
            },
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>Retrieves one ticket with its customer summary and its history, newest first.</summary>
    /// <param name="id">The ticket identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Response<TicketDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<TicketDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTicketByIdQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Raises a ticket against a customer.</summary>
    /// <remarks>
    /// The ticket starts <c>New</c> with no assignee and a generated <c>TKT-nnnnnn</c> reference
    /// (AC-29). An unknown <c>customerId</c> or <c>categoryId</c> is a <b>400 keyed to that field</b>,
    /// not a 404 (AC-31): the ticket collection exists, and it is the payload that is wrong.
    /// </remarks>
    /// <param name="request">Subject, description, customer, category and priority.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateTicketCommand(
                request.Subject,
                request.Description,
                request.CustomerId,
                request.CategoryId,
                request.Impact,
                request.Urgency),
            ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    /// <summary>The support agents a ticket may be assigned to.</summary>
    /// <remarks>
    /// Supervisors only. Exists because the user-administration surface is Admin-only and a
    /// supervisor is not an administrator — so without it the assign picker would have nothing to
    /// offer. Narrow by design: id, name and email, and only users holding the <c>Agent</c> role,
    /// which is the same filter the assignment itself enforces (AC-44).
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("assignable-agents")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(Response<IReadOnlyList<AssignableAgentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAssignableAgents(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAssignableAgentsQuery(), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Moves a ticket along its lifecycle.</summary>
    /// <remarks>
    /// A sub-resource <c>POST</c> rather than a <c>PATCH</c> on the ticket, because a status change
    /// is a **transition**, not a field assignment — <c>PATCH { "status": "Closed" }</c> invites a
    /// client to think it is setting a value, and the transition table refuses that reading.
    ///
    /// A transition outside the table answers <b>409</b>, not 400 (AC-38): the request is
    /// well-formed and it is the state that is wrong. A status that does not exist at all is a 400.
    /// An agent may only move a ticket assigned to them (AC-45, AC-46); a supervisor may move any
    /// (AC-47), and that check is inside the handler because only it has loaded the ticket.
    /// </remarks>
    /// <param name="id">The ticket identifier.</param>
    /// <param name="request">Target status, and the <c>rowVersion</c> read from the detail endpoint.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeTicketStatusRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ChangeTicketStatusCommand(id, request.Status, request.RowVersion,
                request.ResolutionCode, request.ResolutionNotes),
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>Sets a ticket's impact and urgency; priority is re-derived by the matrix (US-923).</summary>
    /// <remarks>
    /// Priority has no direct setter anywhere on the surface (spec decision: matrix-only). A
    /// changed derivation writes a <c>Reprioritized</c> history row; an unchanged one does not.
    /// </remarks>
    /// <param name="id">The ticket identifier.</param>
    /// <param name="request">Impact, urgency, and the <c>rowVersion</c> read from the detail endpoint.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/classification")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reclassify(Guid id, [FromBody] ReclassifyTicketRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ReclassifyTicketCommand(id, request.Impact, request.Urgency, request.RowVersion),
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>Adds a tag to a ticket (US-924). Duplicates, an 11th tag, or a bad value are field-keyed 400s.</summary>
    [HttpPost("{id:guid}/tags")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTag(Guid id, [FromBody] AddTicketTagRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AddTicketTagCommand(id, request.Value), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Removes a tag by its normalized value (US-924).</summary>
    [HttpDelete("{id:guid}/tags/{value}")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTag(Guid id, string value, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveTicketTagCommand(id, value), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Links this ticket to another by reference — RelatedTo or DuplicateOf (US-925).</summary>
    /// <remarks>
    /// An unknown reference is a 400 keyed to <c>targetReference</c>; the same link twice, or two
    /// tickets each DuplicateOf the other, is a 409. Creating a link never resolves anything —
    /// AC-925.3's rule runs on the status endpoint, at resolve time.
    /// </remarks>
    [HttpPost("{id:guid}/links")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddLink(Guid id, [FromBody] AddTicketLinkRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AddTicketLinkCommand(id, request.LinkType, request.TargetReference), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Removes a ticket link by id (US-925).</summary>
    [HttpDelete("{id:guid}/links/{linkId:guid}")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveLink(Guid id, Guid linkId, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveTicketLinkCommand(id, linkId), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Assigns or reassigns a ticket to a support agent.</summary>
    /// <remarks>
    /// <b>Any authenticated user may assign; agents may only assign to themselves (AC-533).</b>
    /// The target must exist <em>and</em> hold the <c>Agent</c> role; anything else is a 400 keyed
    /// to <c>assigneeId</c> (AC-44).
    /// </remarks>
    /// <param name="id">The ticket identifier.</param>
    /// <param name="request">The agent to assign to, and the <c>rowVersion</c> read from the detail endpoint.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/assignee")]
    [Authorize(Policy = "Authenticated")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new AssignTicketCommand(id, request.AssigneeId, request.RowVersion),
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>Hands an escalated ticket to a named Specialist/Supervisor (US-904, AC-506).</summary>
    [HttpPost("{id:guid}/escalation-owner")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TakeEscalation(Guid id, [FromBody] TakeEscalationRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new TakeEscalationCommand(id, request.AssigneeId, request.RowVersion), ct);
        return this.ToActionResult(result);
    }

    /// <summary>A ticket's message timeline, oldest first (AC-106).</summary>
    /// <remarks>
    /// Unpaginated — the same shape the status-history timeline takes, because this is meant to
    /// render in full on one screen (spec A6). An unknown ticket is 404; a ticket with no recorded
    /// messages is 200 with an empty list, not 404 (AC-108).
    /// </remarks>
    /// <param name="id">The ticket identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(Response<IReadOnlyList<TicketMessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<IReadOnlyList<TicketMessageDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTicketMessagesQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Records a message against a ticket — a call, a manually-logged email, a note about contact (AC-101).</summary>
    /// <remarks>
    /// The sender is taken from the session, never from the payload — there is no sender field on
    /// the request record for a client to fill in. <c>Direction</c> distinguishes what the agent is
    /// recording (something the customer said vs. something the agent said); it does not change who
    /// <c>SenderId</c> is, which is always the caller (spec A1).
    /// </remarks>
    /// <param name="id">The ticket the message belongs to.</param>
    /// <param name="request">Direction, channel, optional subject, and the message body.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordMessage(Guid id, [FromBody] RecordTicketMessageRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RecordTicketMessageCommand(id, request.Direction, request.Channel, request.Subject, request.Body),
            ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        // Location points at the list, not at the message — same reasoning as CustomerNotes:
        // there is no single-message route, and AC-106 reads the timeline as a whole.
        return CreatedAtAction(nameof(GetMessages), new { id }, result);
    }

    /// <summary>Links a Published article as the solution to a ticket — AC-178, AC-179, AC-181.</summary>
    [HttpPost("{id:guid}/content/{contentId:guid}")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkContent(Guid id, Guid contentId, CancellationToken ct)
    {
        var result = await mediator.Send(new LinkContentToTicketCommand(id, contentId), ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>Removes an article link — AC-180.</summary>
    [HttpDelete("{id:guid}/content/{contentId:guid}")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkContent(Guid id, Guid contentId, CancellationToken ct)
    {
        var result = await mediator.Send(new UnlinkContentFromTicketCommand(id, contentId), ct);
        return this.ToActionResult(result, StatusCodes.Status204NoContent);
    }

    /// <summary>All articles linked to a ticket — AC-181.</summary>
    [HttpGet("{id:guid}/content")]
    [ProducesResponseType(typeof(Response<IReadOnlyList<LinkedContentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLinkedContent(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetLinkedContentQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Uploads an image/PDF against a ticket (TA-9/TA-11).</summary>
    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(FileStorageOptions.DefaultRequestBodyLimitBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> AddAttachment(Guid id, IFormFile? file, CancellationToken ct)
    {
        await using var content = file?.OpenReadStream() ?? Stream.Null;

        var result = await mediator.Send(
            new AddTicketAttachmentCommand(
                id,
                file?.FileName ?? string.Empty,
                file?.ContentType ?? string.Empty,
                file?.Length ?? 0,
                content),
            ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(ListAttachments), new { id }, result);
    }

    /// <summary>Lists a ticket's attachments (TA-10).</summary>
    [HttpGet("{id:guid}/attachments")]
    [ProducesResponseType(typeof(Response<IReadOnlyList<TicketAttachmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAttachments(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTicketAttachmentsQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Streams a ticket attachment's bytes with its original filename (TA-6, TA-10).</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var result = await mediator.Send(new DownloadTicketAttachmentQuery(id, attachmentId), ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        var content = result.Data!;
        return File(content.Content, content.ContentType, content.OriginalFileName);
    }
}
