using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Reports.Dtos;
using CustomerSupport.Application.Features.Reports.Queries.GetAgentPerformanceReport;
using CustomerSupport.Application.Features.Reports.Queries.GetCsatReport;
using CustomerSupport.Application.Features.Reports.Queries.GetSlaPerformanceReport;
using CustomerSupport.Application.Features.Reports.Queries.GetTicketVolumeReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Reporting — FEAT-19+, AC-148..AC-154. `Supervisor` policy already means "Supervisor or Admin"
/// (see `AuthorizationExtensions.cs`), so it alone satisfies AC-148 without a second role list.
/// </summary>
[ApiController]
[Route("api/reports")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Supervisor")]
public class ReportsController(IMediator mediator) : ControllerBase
{
    /// <summary>Ticket volume by period, category and priority (AC-149..AC-151).</summary>
    /// <param name="from">Start of the date range (inclusive).</param>
    /// <param name="to">End of the date range (inclusive). Must not be before <paramref name="from"/> (AC-154).</param>
    /// <param name="groupBy">day, week or month. Defaults to day.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("ticket-volume")]
    [ProducesResponseType(typeof(Response<TicketVolumeReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<TicketVolumeReportDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTicketVolume(
        [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string groupBy = "day",
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetTicketVolumeReportQuery(from, to, groupBy), ct);
        return this.ToActionResult(result);
    }

    /// <summary>SLA attainment and breach counts by priority (AC-152).</summary>
    /// <param name="from">Start of the date range (inclusive), matched against ticket creation.</param>
    /// <param name="to">End of the date range (inclusive). Must not be before <paramref name="from"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("sla-performance")]
    [ProducesResponseType(typeof(Response<SlaPerformanceReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<SlaPerformanceReportDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSlaPerformance(
        [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetSlaPerformanceReportQuery(from, to), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Customer satisfaction over a period (US-605) — average rating and the
    /// promoters/passives/detractors split from the portal's post-resolution surveys.</summary>
    /// <param name="from">Start of the date range (inclusive), matched against survey submission.</param>
    /// <param name="to">End of the date range (inclusive). Must not be before <paramref name="from"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("csat")]
    [ProducesResponseType(typeof(Response<CsatReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<CsatReportDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCsat(
        [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCsatReportQuery(from, to), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Tickets resolved and approximate handle time per agent (AC-153).</summary>
    /// <param name="from">Start of the date range (inclusive), matched against ticket creation.</param>
    /// <param name="to">End of the date range (inclusive). Must not be before <paramref name="from"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("agent-performance")]
    [ProducesResponseType(typeof(Response<AgentPerformanceReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<AgentPerformanceReportDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAgentPerformance(
        [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetAgentPerformanceReportQuery(from, to), ct);
        return this.ToActionResult(result);
    }
}
