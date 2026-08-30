using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Sla.Commands.CreateBusinessHoursCalendar;
using CustomerSupport.Application.Features.Sla.Commands.CreatePublicHoliday;
using CustomerSupport.Application.Features.Sla.Dtos;
using CustomerSupport.Application.Features.Sla.Queries.GetBusinessHoursCalendars;
using CustomerSupport.Application.Features.Sla.Queries.GetPublicHolidays;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Branch business-hours calendars and public holidays — US-215, `AC-225`..`AC-228`. Admin configures
/// the per-branch working windows and whole-day exclusions that the SLA target calculation (AC-225/226)
/// advances through; the rows are read back via the paged lists. Mirrors <see cref="SLAPoliciesController"/>'s
/// routing and gating.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class BusinessHoursController(IMediator mediator) : ControllerBase
{
    /// <summary>Lists business-hours calendar rows, paginated.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page. Above the server maximum this is a 400 (AC-11).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("calendars")]
    [ProducesResponseType(typeof(Response<PaginatedList<BusinessHoursCalendarDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<BusinessHoursCalendarDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCalendars(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetBusinessHoursCalendarsQuery { PageIndex = page, PageSize = pageSize }, ct);
        return this.ToActionResult(result);
    }

    /// <summary>Records a working window for one weekday in one branch. Admin only (AC-228).</summary>
    /// <param name="request">Branch, day of week, and open/close working times.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("calendars")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCalendar([FromBody] CreateBusinessHoursCalendarRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateBusinessHoursCalendarCommand(
                request.BranchId, request.DayOfWeek, request.OpenTime, request.CloseTime),
            ct);

        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>Lists public holidays, paginated.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("holidays")]
    [ProducesResponseType(typeof(Response<PaginatedList<PublicHolidayDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<PublicHolidayDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHolidays(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetPublicHolidaysQuery { PageIndex = page, PageSize = pageSize }, ct);
        return this.ToActionResult(result);
    }

    /// <summary>Records a whole-day exclusion for one branch. Admin only (AC-228).</summary>
    /// <param name="request">Branch, holiday date, and name.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("holidays")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateHoliday([FromBody] CreatePublicHolidayRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreatePublicHolidayCommand(request.BranchId, request.HolidayDate, request.Name),
            ct);

        return this.ToActionResult(result, StatusCodes.Status201Created);
    }
}
