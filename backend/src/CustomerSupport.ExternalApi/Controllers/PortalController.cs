using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Portal.Commands.CreatePortalReply;
using CustomerSupport.Application.Features.Portal.Commands.SubmitSurvey;
using CustomerSupport.Application.Features.Portal.Dtos;
using CustomerSupport.Application.Features.Auth.Commands.UpdateCurrentUserProfile;
using CustomerSupport.Application.Features.Auth.Queries.GetCurrentUser;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Application.Features.Portal.Queries.GetPortalTicketDetail;
using CustomerSupport.Application.Features.Portal.Queries.GetPortalTickets;
using CustomerSupport.Application.Features.Tickets.Commands.AddTicketAttachment;
using CustomerSupport.Application.Features.Tickets.Commands.CreateTicket;
using CustomerSupport.Application.Features.Tickets.Dtos;
using CustomerSupport.Application.Features.Tickets.Queries.DownloadTicketAttachment;
using CustomerSupport.Application.Features.Tickets.Queries.GetTicketAttachments;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// The customer portal surface — the tickets a signed-in customer owns, plus replying and surveying
/// them (US-404..US-409, PJ-5..PJ-12). Every action requires the <c>customerId</c> claim; a request
/// reaching here without one (a staff token, or any non-portal account) is refused with 403, because
/// the surface is meaningless for anyone who is not a linked customer.
/// </summary>
[ApiController]
[Route("api/portal")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class PortalController(IMediator mediator) : ControllerBase
{
    private const string PortalSource = "Portal";

    /// <summary>Gets the signed-in customer's profile through the portal surface.</summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(Response<UserInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        if (RequireCustomerId(out _, out var forbidden))
        {
            return forbidden;
        }

        var result = await mediator.Send(new GetCurrentUserQuery(), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Updates the signed-in customer's own profile.</summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(Response<UserInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateCurrentUserProfileRequest request, CancellationToken ct)
    {
        if (RequireCustomerId(out _, out var forbidden))
        {
            return forbidden;
        }

        var result = await mediator.Send(
            new UpdateCurrentUserProfileCommand(
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.ProfileImageUrl),
            ct);
        return this.ToActionResult(result);
    }

    /// <summary>Raises a new ticket for the signed-in customer (US-404, PJ-5).</summary>
    [HttpPost("tickets")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTicket(
        [FromBody] PortalCreateTicketRequest request, CancellationToken ct)
    {
        if (RequireCustomerId(out var customerId, out var forbidden))
        {
            return forbidden;
        }

        var command = new CreateTicketCommand(
            request.Subject,
            request.Description,
            customerId,
            request.CategoryId,
            Source: PortalSource);

        var result = await mediator.Send(command, ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>Lists the signed-in customer's own tickets, newest first (US-405, PJ-8).</summary>
    [HttpGet("tickets")]
    [ProducesResponseType(typeof(Response<IReadOnlyList<PortalTicketListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTickets(CancellationToken ct)
    {
        if (RequireCustomerId(out var customerId, out var forbidden))
        {
            return forbidden;
        }

        var result = await mediator.Send(new GetPortalTicketsQuery(customerId), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Gets one of the signed-in customer's tickets (US-406, PJ-9).</summary>
    [HttpGet("tickets/{id:guid}")]
    [ProducesResponseType(typeof(Response<PortalTicketDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicket(Guid id, CancellationToken ct)
    {
        if (RequireCustomerId(out var customerId, out var forbidden))
        {
            return forbidden;
        }

        var result = await mediator.Send(new GetPortalTicketDetailQuery(id, customerId), ct);
        return this.ToActionResult(result);
    }

    /// <summary>A customer replies to their own ticket (US-407, PJ-10).</summary>
    [HttpPost("tickets/{id:guid}/reply")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reply(
        Guid id, [FromBody] PortalReplyRequest request, CancellationToken ct)
    {
        if (RequireCustomerId(out var customerId, out var forbidden))
        {
            return forbidden;
        }

        var command = new CreatePortalReplyCommand(id, request.Body, customerId);
        var result = await mediator.Send(command, ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>A customer submits a satisfaction survey for a resolved ticket (US-408/US-409, PJ-11/12).</summary>
    [HttpPost("tickets/{id:guid}/survey")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitSurvey(
        Guid id, [FromBody] PortalSurveyRequest request, CancellationToken ct)
    {
        if (RequireCustomerId(out var customerId, out var forbidden))
        {
            return forbidden;
        }

        var command = new SubmitSurveyCommand(id, request.Rating, request.Comment, customerId);
        var result = await mediator.Send(command, ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>Uploads an image/PDF against the signed-in customer's own ticket (TA-1..TA-6).</summary>
    [HttpPost("tickets/{id:guid}/attachments")]
    [RequestSizeLimit(FileStorageOptions.DefaultRequestBodyLimitBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> AddTicketAttachment(
        Guid id, IFormFile? file, CancellationToken ct)
    {
        if (RequireCustomerId(out var customerId, out var forbidden))
        {
            return forbidden;
        }

        await using var content = file?.OpenReadStream() ?? Stream.Null;

        var result = await mediator.Send(
            new AddTicketAttachmentCommand(
                id,
                file?.FileName ?? string.Empty,
                file?.ContentType ?? string.Empty,
                file?.Length ?? 0,
                content,
                customerId),
            ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(ListTicketAttachments), new { id }, result);
    }

    /// <summary>Lists the signed-in customer's ticket attachments (TA-7, US-406).</summary>
    [HttpGet("tickets/{id:guid}/attachments")]
    [ProducesResponseType(typeof(Response<IReadOnlyList<TicketAttachmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListTicketAttachments(Guid id, CancellationToken ct)
    {
        if (RequireCustomerId(out var customerId, out var forbidden))
        {
            return forbidden;
        }

        var result = await mediator.Send(new GetTicketAttachmentsQuery(id, customerId), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Streams an attachment of the signed-in customer's own ticket (TA-5, TA-6, TA-7).</summary>
    [HttpGet("tickets/{id:guid}/attachments/{attachmentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadTicketAttachment(
        Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (RequireCustomerId(out var customerId, out var forbidden))
        {
            return forbidden;
        }

        var result = await mediator.Send(
            new DownloadTicketAttachmentQuery(id, attachmentId, customerId), ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        var content = result.Data!;
        return File(content.Content, content.ContentType, content.OriginalFileName);
    }

    /// <summary>Resolves the <c>customerId</c> claim, or returns a 403 envelope when it is absent.</summary>
    private bool RequireCustomerId(out Guid customerId, out IActionResult forbidden)
    {
        var id = User.GetCustomerId();
        if (id is { } cid)
        {
            customerId = cid;
            forbidden = null!;
            return false;
        }

        customerId = Guid.Empty;
        forbidden = this.ToActionResult(
            Response<Unit>.Fail(
                ApplicationErrors.General.FORBIDDEN,
                ApplicationErrors.General.FORBIDDEN,
                MessageType.Forbidden));
        return true;
    }
}

public record PortalCreateTicketRequest(string Subject, string Description, Guid CategoryId);

public record PortalReplyRequest(string Body);

public record PortalSurveyRequest(int Rating, string? Comment);
