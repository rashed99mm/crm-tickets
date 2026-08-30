using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Admin.Dtos;
using CustomerSupport.Application.Features.Admin.Queries.GetAuditLog;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Administrative surfaces that don't belong to any one domain area — FEAT-21, `US-801`. Admin
/// only, at the controller level: unlike the ticket workflow's per-record authorization, there is
/// no "which admin's audit log is this" question — the whole trail is one Admin-only resource.
/// </summary>
[ApiController]
[Route("api/admin")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "UserManagement")]
public class AdminController(IMediator mediator) : ControllerBase
{
    /// <summary>The audit trail, newest first (AC-140), optionally filtered.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page. Above the server maximum this is a 400 (AC-11).</param>
    /// <param name="actionType">Exact match against the recorded action (e.g. "Created").</param>
    /// <param name="userId">Only entries recorded against this user.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("audit-log")]
    [ProducesResponseType(typeof(Response<PaginatedList<AuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<AuditLogDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? actionType = null,
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetAuditLogQuery { PageIndex = page, PageSize = pageSize, ActionType = actionType, UserId = userId },
            ct);

        return this.ToActionResult(result);
    }
}
