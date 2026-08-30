using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Customers.Commands.AddCustomerAttachment;
using CustomerSupport.Application.Features.Customers.Commands.AddCustomerNote;
using CustomerSupport.Application.Features.Customers.Commands.CreateCustomer;
using CustomerSupport.Application.Features.Customers.Commands.DeleteCustomer;
using CustomerSupport.Application.Features.Customers.Commands.RemoveCustomerAttachment;
using CustomerSupport.Application.Features.Customers.Commands.UpdateCustomer;
using CustomerSupport.Application.Features.Customers.Dtos;
using CustomerSupport.Application.Features.Customers.Queries.DownloadCustomerAttachment;
using CustomerSupport.Application.Features.Customers.Queries.GetCustomerAttachments;
using CustomerSupport.Application.Features.Customers.Queries.GetCustomerById;
using CustomerSupport.Application.Features.Customers.Queries.GetCustomerNotes;
using CustomerSupport.Application.Features.Customers.Queries.GetCustomers;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// The people who contact support. FEAT-03, criteria AC-7 through AC-16.
/// </summary>
/// <remarks>
/// Every action requires a session (AC-3). No role policy beyond that: the slice specification
/// places no role restriction on customer management, and inventing one would be a control nothing
/// asked for.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class CustomersController(IMediator mediator) : ControllerBase
{
    /// <summary>Lists customers, newest page first, optionally filtered by a search term.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page. Above the server maximum this is a 400 (AC-11).</param>
    /// <param name="search">Matches name or email, case-insensitively (AC-13).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(Response<PaginatedList<CustomerDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<CustomerDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetCustomersQuery { PageIndex = page, PageSize = pageSize, Search = search },
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>Retrieves one customer. An unknown or deleted id is a 404 (AC-12).</summary>
    /// <param name="id">The customer identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Response<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<CustomerDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCustomerByIdQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Records a new customer.</summary>
    /// <remarks>
    /// An email already in use answers <b>409</b>, not 400 (AC-9): the request is well formed and it
    /// is the state of the world that refuses it.
    /// </remarks>
    /// <param name="request">Name, email and optional phone.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateCustomerCommand(request.Name, request.Email, request.Phone),
            ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        // AC-7 wants a Location header, so this cannot go through ToActionResult's 201 branch.
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    /// <summary>Corrects a customer record. Validation matches creation's (AC-14).</summary>
    /// <param name="id">The customer identifier.</param>
    /// <param name="request">The corrected values.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateCustomerCommand(id, request.Name, request.Email, request.Phone),
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>Removes a customer.</summary>
    /// <remarks>
    /// Refused with <b>409</b> if the customer holds any ticket (AC-15) — support history is not
    /// destroyable by one click. Otherwise soft-deleted and answered <b>200</b>, not 204, so the
    /// response still carries a code and a message.
    /// </remarks>
    /// <param name="id">The customer identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteCustomerCommand(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Lists a customer's interaction history, newest first (AC-74).</summary>
    /// <remarks>
    /// Each note carries its author's name, resolved at read time from the stored author id — the
    /// row holds no name, because notes are never edited and a copied name could never be corrected.
    /// An unknown customer is a 404: the customer is named in the path, so its absence makes the
    /// addressed resource absent rather than the page empty.
    /// </remarks>
    /// <param name="id">The customer identifier.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page. Above the server maximum this is a 400 (AC-11).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}/notes")]
    [ProducesResponseType(typeof(Response<PaginatedList<CustomerNoteDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<PaginatedList<CustomerNoteDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<PaginatedList<CustomerNoteDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotes(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetCustomerNotesQuery { CustomerId = id, PageIndex = page, PageSize = pageSize },
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>Appends an interaction note to a customer (AC-75).</summary>
    /// <remarks>
    /// The author is taken from the session and never from the payload (AC-76) — the request record
    /// has no author field for a client to fill in. An empty or whitespace-only body is a 400 keyed
    /// to <c>Body</c>, so the note box can show the message on itself.
    /// </remarks>
    /// <param name="id">The customer the note belongs to.</param>
    /// <param name="request">The note text.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/notes")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddNote(
        Guid id,
        [FromBody] CreateCustomerNoteRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new AddCustomerNoteCommand(id, request.Body), ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        // Location points at the list rather than at the note: there is no single-note route, and
        // AC-74 says the history is read as a whole.
        return CreatedAtAction(nameof(GetNotes), new { id }, result);
    }

    /// <summary>Lists a customer's attachments, newest first (AC-22, AC-83).</summary>
    /// <remarks>
    /// Each row carries the <em>original</em> filename, its content type, its size and who uploaded
    /// it. The name the file has on disk is deliberately not published: it is of no use to a client
    /// and it is the one string a traversal attempt would want.
    /// </remarks>
    /// <param name="id">The customer identifier.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page. Above the server maximum this is a 400 (AC-11).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}/attachments")]
    [ProducesResponseType(typeof(Response<PaginatedList<CustomerAttachmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<PaginatedList<CustomerAttachmentDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<PaginatedList<CustomerAttachmentDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachments(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetCustomerAttachmentsQuery { CustomerId = id, PageIndex = page, PageSize = pageSize },
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>Stores a file against a customer (AC-22).</summary>
    /// <remarks>
    /// Three refusals, in this order, each before the next cost is incurred: an unknown customer is
    /// a <b>404</b> (AC-27); a file over 10 MB is a <b>413</b> decided from the declared length
    /// before the stream is read, so nothing reaches the disk (AC-23); and a content type outside
    /// the allowlist is a <b>415</b>, again before any write (AC-24).
    ///
    /// The uploader comes from the session, never from the form. The name on disk is generated by
    /// the domain from a GUID, so a filename containing <c>../</c> is metadata and not a path
    /// (AC-25).
    /// </remarks>
    /// <param name="id">The customer the file belongs to.</param>
    /// <param name="file">The uploaded file, as <c>multipart/form-data</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/attachments")]
    // Set deliberately, and a little above the 10 MB rule to leave room for the multipart envelope.
    // The point is that Kestrel's 30 MB default is not what silently enforces AC-23 — a limit
    // nobody chose is a limit nobody can change on purpose. The handler still decides the answer.
    [RequestSizeLimit(FileStorageOptions.DefaultRequestBodyLimitBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> AddAttachment(Guid id, IFormFile? file, CancellationToken ct)
    {
        // The stream is passed unread. file.Length is the declared length the size check uses, and
        // opening the stream here rather than in the handler keeps IFormFile — an ASP.NET type —
        // out of Application, which references no web framework at all.
        //
        // A request with no file part at all goes through as a zero length, so it takes the
        // handler's empty-file branch and comes back as the envelope with a bilingual code, rather
        // than as the ProblemDetails a binding failure would produce.
        await using var content = file?.OpenReadStream() ?? Stream.Null;

        var result = await mediator.Send(
            new AddCustomerAttachmentCommand(
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

        // Location points at the list, as the notes route does: there is no single-attachment
        // metadata route, only a content one.
        return CreatedAtAction(nameof(GetAttachments), new { id }, result);
    }

    /// <summary>Streams one attachment's bytes back (AC-26).</summary>
    /// <remarks>
    /// Streamed by this action rather than served from a static path, and that is the criterion
    /// rather than a preference: a static path is a URL, and a URL is reachable without a session.
    /// <c>Content-Disposition</c> carries the original filename, so what a browser saves is a name
    /// a human recognises rather than the GUID on disk.
    /// </remarks>
    /// <param name="id">The customer the attachment belongs to.</param>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var result = await mediator.Send(new DownloadCustomerAttachmentQuery(id, attachmentId), ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        // FileStreamResult disposes the stream once the response has been written.
        var content = result.Data!;
        return File(content.Content, content.ContentType, content.OriginalFileName);
    }

    /// <summary>Removes an attachment — the row and the file (AC-28).</summary>
    /// <remarks>
    /// Both rows are soft-deleted and only then is the file removed. Row first, file second: a live
    /// file with no row is invisible and reclaimable, while a deleted file with a live row is a
    /// download that fails from a list still claiming the file is there.
    ///
    /// Answered <b>200</b> rather than 204, so the response still carries a code and a message.
    /// </remarks>
    /// <param name="id">The customer the attachment belongs to.</param>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveCustomerAttachmentCommand(id, attachmentId), ct);
        return this.ToActionResult(result);
    }
}
