using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.Models.SubscriptionPlanQuotas;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

internal static class SubscriptionPlanQuotaHttpResultMapper
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        SubscriptionPlanQuotaOperationResult<T> result,
        string successMessage = "OK")
    {
        if (result.Success)
        {
            return controller.Ok(new ApiResponse<T>
            {
                Success = true,
                Message = successMessage,
                Data = result.Data,
            });
        }

        return controller.StatusCode(ToStatusCode(result.Error), new ApiResponse<T>
        {
            Success = false,
            Message = result.Message ?? "Subscription plan quota request failed.",
            Errors = new List<string> { ToStableCode(result.Error) },
        });
    }

    private static int ToStatusCode(SubscriptionPlanQuotaErrorCode error)
    {
        return error switch
        {
            SubscriptionPlanQuotaErrorCode.InvalidRequest =>
                StatusCodes.Status400BadRequest,
            SubscriptionPlanQuotaErrorCode.SubscriptionPlanNotFound
                or SubscriptionPlanQuotaErrorCode.QuotaNotFound
                or SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaNotFound =>
                StatusCodes.Status404NotFound,
            _ => StatusCodes.Status409Conflict,
        };
    }

    private static string ToStableCode(SubscriptionPlanQuotaErrorCode error)
    {
        return error switch
        {
            SubscriptionPlanQuotaErrorCode.InvalidRequest =>
                "INVALID_REQUEST",
            SubscriptionPlanQuotaErrorCode.SubscriptionPlanNotFound =>
                "SUBSCRIPTION_PLAN_NOT_FOUND",
            SubscriptionPlanQuotaErrorCode.QuotaNotFound =>
                "QUOTA_NOT_FOUND",
            SubscriptionPlanQuotaErrorCode.QuotaInactive =>
                "QUOTA_INACTIVE",
            SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaNotFound =>
                "SUBSCRIPTION_PLAN_QUOTA_NOT_FOUND",
            _ => "SUBSCRIPTION_PLAN_QUOTA_CONFLICT",
        };
    }
}
