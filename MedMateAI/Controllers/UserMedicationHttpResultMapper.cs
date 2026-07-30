using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.Models.UserMedications;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

internal static class UserMedicationHttpResultMapper
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        UserMedicationOperationResult<T> result)
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

    public static IActionResult ToCreatedResult<T>(
        this ControllerBase controller,
        UserMedicationOperationResult<T> result)
    {
        if (!result.Success)
        {
            return controller.ToActionResult(result);
        }

        return controller.StatusCode(StatusCodes.Status201Created, new ApiResponse<T>
        {
            Success = true,
            Message = "User medication created.",
            Data = result.Data
        });
    }

    public static IActionResult MedicationUnauthorizedResult(
        this ControllerBase controller)
    {
        return controller.Unauthorized(new ApiResponse
        {
            Success = false,
            Message = "Unauthorized.",
            Errors = new List<string> { "UNAUTHENTICATED" }
        });
    }

    private static int ToStatusCode(UserMedicationErrorCode error)
    {
        return error switch
        {
            UserMedicationErrorCode.Unauthenticated =>
                StatusCodes.Status401Unauthorized,
            UserMedicationErrorCode.InvalidRequest =>
                StatusCodes.Status400BadRequest,
            UserMedicationErrorCode.NotFound =>
                StatusCodes.Status404NotFound,
            _ => StatusCodes.Status409Conflict
        };
    }

    private static string ToStableCode(UserMedicationErrorCode error)
    {
        return string.Concat(error.ToString().Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $"_{character}"
                : character.ToString()))
            .ToUpperInvariant();
    }
}
