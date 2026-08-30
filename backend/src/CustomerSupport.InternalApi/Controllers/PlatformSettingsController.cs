using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Localization;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Application.Features.PlatformSettings.Commands.CreatePlatformSetting;
using CustomerSupport.Application.Features.PlatformSettings.Commands.DeletePlatformSetting;
using CustomerSupport.Application.Features.PlatformSettings.Commands.UpdatePlatformSetting;
using PlatformSettingDto = CustomerSupport.Application.Features.PlatformSettings.Dtos.PlatformSettingDto;
using CustomerSupport.Application.Features.PlatformSettings.Queries.GetPlatformSettingByKey;
using CustomerSupport.Application.Features.PlatformSettings.Queries.GetPlatformSettings;
using CustomerSupport.Application.Features.PlatformSettings.Queries.GetPlatformSettingById;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using CustomerSupport.Application.Features.PlatformSettings.Commands.UpdateBranding;
using CustomerSupport.Application.Features.PlatformSettings.Queries.GetBranding;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Asp.Versioning;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Manages platform-wide configuration settings.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class PlatformSettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<PlatformSettingsController> _logger;

    public PlatformSettingsController(IMediator mediator, ILocalizationService localizationService, ILogger<PlatformSettingsController> logger)
    {
        _mediator = mediator;
        _localizationService = localizationService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all platform settings with pagination and filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(Response<PaginatedList<PlatformSettingDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] string? key = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Platform settings list requested");

        var query = new GetPlatformSettingsQuery(null)
        {
            PageIndex = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
            IncludePrivate = User.IsInRole("Admin") || User.IsInRole("SuperAdmin"),
            Key = key
        };
        
        var result = await _mediator.Send(query, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Retrieves platform settings filtered by category.
    /// </summary>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(Response<PaginatedList<PlatformSettingDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByCategory(
        string category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "asc",
        CancellationToken ct = default)
    {
        _logger.LogInformation("Platform settings for category {Category} requested", category);

        var query = new GetPlatformSettingsQuery(category)
        {
            PageIndex = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
            IncludePrivate = User.IsInRole("Admin") || User.IsInRole("SuperAdmin")
        };
        
        var result = await _mediator.Send(query, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Retrieves a specific platform setting by key.
    /// </summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(Response<PlatformSettingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByKey(string key, CancellationToken ct)
    {
        _logger.LogInformation("Platform setting {SettingKey} requested", key);

        var result = await _mediator.Send(
            new GetPlatformSettingByKeyQuery(key, User.IsInRole("Admin") || User.IsInRole("SuperAdmin")), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Creates a new platform setting.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreatePlatformSettingRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Platform setting creation requested for key {SettingKey}", request.Key);

        var command = new CreatePlatformSettingCommand(
            request.Key,
            request.Value,
            request.Description,
            request.Category,
            request.ValueType,
            request.IsEncrypted,
            request.IsPublic
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Updates an existing platform setting by key.
    /// </summary>
    [HttpPut("{key}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(string key, [FromBody] UpdatePlatformSettingRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Update requested for platform setting {SettingKey}", key);

        var getResult = await _mediator.Send(
            new GetPlatformSettingByKeyQuery(key, true), ct);
        
        if (!getResult.Success || getResult.Data == null)
        {
            var localized = _localizationService.GetLocalizedMessage(ApplicationErrors.PlatformSetting.NOT_FOUND);
            return this.ToActionResult(Response<PlatformSettingDto>.Fail(
                ApplicationErrors.PlatformSetting.NOT_FOUND,
                localized.En,
                MessageType.NotFound));
        }

        var command = new UpdatePlatformSettingCommand(
            getResult.Data.Id,
            request.Value,
            request.Description,
            null,
            null
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Updates a setting by its stable database identifier. The UI uses this route so keys may
    /// safely contain punctuation without depending on route-value encoding.
    /// </summary>
    [HttpPut("id/{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateById(Guid id, [FromBody] UpdatePlatformSettingRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdatePlatformSettingCommand(id, request.Value, request.Description, null, null), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Soft-deletes a platform setting by key.
    /// </summary>
    [HttpDelete("{key}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(string key, CancellationToken ct)
    {
        _logger.LogInformation("Delete requested for platform setting {SettingKey}", key);

        var getResult = await _mediator.Send(
            new GetPlatformSettingByKeyQuery(key, true), ct);
        
        if (!getResult.Success || getResult.Data == null)
        {
            var localized = _localizationService.GetLocalizedMessage(ApplicationErrors.PlatformSetting.NOT_FOUND);
            return this.ToActionResult(CustomerSupport.Application.Contracts.Response.Fail(
                ApplicationErrors.PlatformSetting.NOT_FOUND,
                localized.En,
                MessageType.NotFound));
        }

        var result = await _mediator.Send(new DeletePlatformSettingCommand(getResult.Data.Id), ct);
        return this.ToActionResult(result, StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Returns current branding configuration (logo, colours). Public — used by both shells on load.
    /// </summary>
    [HttpGet("branding")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<BrandingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBranding(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBrandingQuery(), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Updates branding configuration. Admin only.
    /// </summary>
    [HttpPut("branding")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(Response<BrandingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateBranding([FromBody] UpdateBrandingRequest request, CancellationToken ct)
    {
        var command = new UpdateBrandingCommand(request.LogoUrl, request.PrimaryColor, request.AccentColor);
        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }
}

public record UpdateBrandingRequest(string LogoUrl, string PrimaryColor, string AccentColor);
