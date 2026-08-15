using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.Models.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

internal static class AnalyticsHttpResultMapper
{
    public static IActionResult ToAnalyticsActionResult<T>(
        this ControllerBase controller,
        AnalyticsOperationResult<T> result)
    {
        if (result.Success)
        {
            return controller.Ok(new ApiResponse<T>
            {
                Success = true,
                Message = result.Message ?? "OK",
                Data = result.Data
            });
        }

        return controller.StatusCode(ToStatusCode(result.Error), new ApiResponse<T>
        {
            Success = false,
            Message = result.Message ?? "Analytics request failed.",
            Errors = new List<string> { ToStableCode(result.Error) }
        });
    }

    public static IActionResult AnalyticsUnauthorized(this ControllerBase controller) =>
        controller.Unauthorized(new ApiResponse
        {
            Success = false,
            Message = "Unauthorized.",
            Errors = new List<string> { "UNAUTHENTICATED" }
        });

    private static int ToStatusCode(AnalyticsErrorCode error) => error switch
    {
        AnalyticsErrorCode.InvalidRequest
            or AnalyticsErrorCode.InvalidDateRange => StatusCodes.Status400BadRequest,
        AnalyticsErrorCode.LabTestTrendNotFound
            or AnalyticsErrorCode.DoctorProfileNotFound => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status409Conflict
    };

    private static string ToStableCode(AnalyticsErrorCode error) => error switch
    {
        AnalyticsErrorCode.InvalidRequest => "INVALID_REQUEST",
        AnalyticsErrorCode.InvalidDateRange => "INVALID_DATE_RANGE",
        AnalyticsErrorCode.LabTestTrendNotFound => "LAB_TEST_TREND_NOT_FOUND",
        AnalyticsErrorCode.DoctorProfileNotFound => "DOCTOR_PROFILE_NOT_FOUND",
        _ => "ANALYTICS_CONFLICT"
    };
}
