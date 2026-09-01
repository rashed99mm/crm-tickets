using System.Diagnostics;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;

using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.Api.Shared.Extensions;

public static class ResponseExtensions
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        Response<T> response,
        int successStatusCode = StatusCodes.Status200OK)
    {
        response = response with { TraceId = Activity.Current?.Id ?? controller.HttpContext.TraceIdentifier };

        if (response.Success)
        {
            if (typeof(T) == typeof(Unit) && successStatusCode == StatusCodes.Status204NoContent)
            {
                return controller.NoContent();
            }

            return successStatusCode switch
            {
                StatusCodes.Status201Created => controller.StatusCode(StatusCodes.Status201Created, response),
                StatusCodes.Status204NoContent => controller.NoContent(),
                _ => controller.StatusCode(successStatusCode, response)
            };
        }

        return controller.StatusCode(MapFailureStatusCode(response.Code), response);
    }

    private static int MapFailureStatusCode(string code) => code switch
    {
        SystemCode.ERR004 or SystemCode.ERR016 => StatusCodes.Status403Forbidden,
        SystemCode.ERR003 or SystemCode.ERR026 or SystemCode.ERR067 => StatusCodes.Status401Unauthorized,
        SystemCode.ERR001 or SystemCode.ERR007 or SystemCode.ERR010 or SystemCode.ERR020
            or SystemCode.ERR030 or SystemCode.ERR032 or SystemCode.ERR034
            or SystemCode.ERR039 or SystemCode.ERR041 or SystemCode.ERR047
            or SystemCode.ERR048 or SystemCode.ERR051 or SystemCode.ERR057
            or SystemCode.ERR060 or SystemCode.ERR080 or SystemCode.ERR086 => StatusCodes.Status404NotFound,
        SystemCode.ERR002 or SystemCode.ERR008 or SystemCode.ERR009
            or SystemCode.ERR013 or SystemCode.ERR014 or SystemCode.ERR015
            or SystemCode.ERR021 or SystemCode.ERR022 or SystemCode.ERR031
            or SystemCode.ERR035 or SystemCode.ERR040 or SystemCode.ERR049
            or SystemCode.ERR050 or SystemCode.ERR055 or SystemCode.ERR056
            or SystemCode.ERR058 or SystemCode.ERR059 or SystemCode.ERR075
            or SystemCode.ERR082 or SystemCode.ERR083 or SystemCode.ERR085
            or SystemCode.ERR087 => StatusCodes.Status409Conflict,
        SystemCode.ERR074 => StatusCodes.Status429TooManyRequests,
        SystemCode.VAL001 => StatusCodes.Status400BadRequest,
        SystemCode.ERR042 or SystemCode.ERR045 => StatusCodes.Status413PayloadTooLarge,
        SystemCode.ERR043 or SystemCode.ERR046 => StatusCodes.Status415UnsupportedMediaType,
        // FEAT-21 A2 — degraded AI capability is a service-availability answer, not a client error.
        // ERR070/ERR071 keep that contract for the resilient provider chain (AI-32).
        SystemCode.ERR052 or SystemCode.ERR070 or SystemCode.ERR071
            => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest
    };
}
