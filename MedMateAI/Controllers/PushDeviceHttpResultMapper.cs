using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.Models.PushDevices;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

internal static class PushDeviceHttpResultMapper
{
    public static IActionResult ToPushDeviceActionResult<T>(
        this ControllerBase controller,
        PushDeviceOperationResult<T> result)
    {
        if (result.Success)
        {
            return controller.Ok(new ApiResponse<T>
            {
                Success = true,
                Message = "OK",
                Data = result.Data
            });
        }

        return controller.StatusCode(ToStatusCode(result.Error), new ApiResponse<T>
        {
            Success = false,
            Message = result.Message ?? "Request failed.",
            Errors = new List<string> { ToStableCode(result.Error) }
        });
    }

    public static IActionResult PushDeviceUnauthorizedResult(
        this ControllerBase controller)
    {
        return controller.Unauthorized(new ApiResponse
        {
            Success = false,
            Message = "Unauthorized.",
            Errors = new List<string> { "UNAUTHENTICATED" }
        });
    }

    private static int ToStatusCode(PushDeviceErrorCode error)
    {
        return error switch
        {
            PushDeviceErrorCode.Unauthenticated =>
                StatusCodes.Status401Unauthorized,
            PushDeviceErrorCode.InvalidRequest =>
                StatusCodes.Status400BadRequest,
            PushDeviceErrorCode.NotFound =>
                StatusCodes.Status404NotFound,
            _ => StatusCodes.Status409Conflict
        };
    }

    private static string ToStableCode(PushDeviceErrorCode error)
    {
        return string.Concat(error.ToString().Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $"_{character}"
                : character.ToString()))
            .ToUpperInvariant();
    }
}
