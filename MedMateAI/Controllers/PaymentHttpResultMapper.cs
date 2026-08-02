using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Payments.Responses;
using MedMateAI.Application.Models.Payments;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

internal static class PaymentHttpResultMapper
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        PaymentReconciliationResult<T> result)
    {
        if (result.Success)
        {
            return controller.Ok(new ApiResponse<T>
            {
                Success = true,
                Message = ResolveSuccessMessage(result.Data),
                Data = result.Data,
            });
        }

        return controller.StatusCode(ToStatusCode(result.Error), new ApiResponse<T>
        {
            Success = false,
            Message = result.Message ?? "Payment reconciliation failed.",
            Errors = new List<string> { ToStableCode(result.Error) },
        });
    }

    private static string ResolveSuccessMessage<T>(T? data)
    {
        if (data is PayOSPaymentStatusResponse status)
        {
            return status.Message;
        }

        return "OK";
    }

    private static int ToStatusCode(PaymentReconciliationErrorCode error)
    {
        return error switch
        {
            PaymentReconciliationErrorCode.Unauthenticated =>
                StatusCodes.Status401Unauthorized,
            PaymentReconciliationErrorCode.InvalidRequest =>
                StatusCodes.Status400BadRequest,
            PaymentReconciliationErrorCode.Forbidden =>
                StatusCodes.Status403Forbidden,
            PaymentReconciliationErrorCode.NotFound
                or PaymentReconciliationErrorCode.ProviderNotFound =>
                StatusCodes.Status404NotFound,
            PaymentReconciliationErrorCode.ProviderRateLimited =>
                StatusCodes.Status429TooManyRequests,
            PaymentReconciliationErrorCode.ProviderUnavailable
                or PaymentReconciliationErrorCode.ProviderInvalidResponse =>
                StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status409Conflict,
        };
    }

    private static string ToStableCode(PaymentReconciliationErrorCode error)
    {
        return error switch
        {
            PaymentReconciliationErrorCode.Unauthenticated => "UNAUTHENTICATED",
            PaymentReconciliationErrorCode.InvalidRequest => "INVALID_REQUEST",
            PaymentReconciliationErrorCode.NotFound => "PAYMENT_NOT_FOUND",
            PaymentReconciliationErrorCode.Forbidden => "PAYMENT_FORBIDDEN",
            PaymentReconciliationErrorCode.ProviderNotFound => "PAYOS_PAYMENT_NOT_FOUND",
            PaymentReconciliationErrorCode.ProviderRateLimited => "PAYOS_RATE_LIMITED",
            PaymentReconciliationErrorCode.ProviderUnavailable => "PAYOS_UNAVAILABLE",
            PaymentReconciliationErrorCode.ProviderInvalidResponse => "PAYOS_INVALID_RESPONSE",
            PaymentReconciliationErrorCode.OrderCodeMismatch => "ORDER_CODE_MISMATCH",
            PaymentReconciliationErrorCode.AmountMismatch => "AMOUNT_MISMATCH",
            _ => "PAYMENT_CONFLICT",
        };
    }
}
