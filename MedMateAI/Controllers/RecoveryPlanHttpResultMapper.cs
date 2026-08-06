using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

internal static class RecoveryPlanHttpResultMapper
{
    public const string UnauthenticatedCode = "UNAUTHENTICATED";

    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        RecoveryPlanOperationResult<T> result)
    {
        if (result.Success)
        {
            return controller.Ok(new ApiResponse<T>
            {
                Success = true,
                Message = result.Message
                    ?? (result.IsReplay ? "Idempotent replay." : "OK"),
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

    public static IActionResult UnauthorizedResult(this ControllerBase controller) =>
        controller.Unauthorized(new ApiResponse
        {
            Success = false,
            Message = "Unauthorized.",
            Errors = new List<string> { UnauthenticatedCode }
        });

    public static IActionResult ToCreatedResult<T>(
        this ControllerBase controller,
        RecoveryPlanOperationResult<T> result,
        string createdMessage)
    {
        if (!result.Success || result.IsReplay)
        {
            return controller.ToActionResult(result);
        }

        return controller.StatusCode(StatusCodes.Status201Created, new ApiResponse<T>
        {
            Success = true,
            Message = createdMessage,
            Data = result.Data
        });
    }

    public static int ToStatusCode(RecoveryPlanErrorCode error) => error switch
    {
        RecoveryPlanErrorCode.Unauthenticated => StatusCodes.Status401Unauthorized,
        RecoveryPlanErrorCode.Forbidden or RecoveryPlanErrorCode.NoActiveSubscription
            or RecoveryPlanErrorCode.RecoveryPlanQuotaNotConfigured
            or RecoveryPlanErrorCode.DoctorNotActive
            or RecoveryPlanErrorCode.DoctorNotAcceptingRequests => StatusCodes.Status403Forbidden,
        RecoveryPlanErrorCode.NotFound
            or RecoveryPlanErrorCode.DoctorProfileNotFound => StatusCodes.Status404NotFound,
        RecoveryPlanErrorCode.InvalidRequest
            or RecoveryPlanErrorCode.IdempotencyKeyInvalid
            or RecoveryPlanErrorCode.RecoveryPlanIncomplete
            or RecoveryPlanErrorCode.InvalidPlanStructure => StatusCodes.Status400BadRequest,
        RecoveryPlanErrorCode.RecoveryPlanWorkflowAlreadyActive
            or RecoveryPlanErrorCode.RecoveryPlanNotCancellable =>
            StatusCodes.Status409Conflict,
        _ => StatusCodes.Status409Conflict
    };

    public static string ToStableCode(RecoveryPlanErrorCode error) =>
        string.Concat(error.ToString().Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $"_{character}" : character.ToString()))
            .ToUpperInvariant();
}
