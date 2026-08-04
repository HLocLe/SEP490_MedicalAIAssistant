using MedMateAI.Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Helpers;

internal static class ApiResponseFactory
{
    public static ApiResponse<T> Success<T>(T data, string message) => new()
    {
        Success = true,
        Message = message,
        Data = data,
    };

    public static ApiResponse Fail(string message) => new()
    {
        Success = false,
        Message = message,
    };

    public static ApiResponse<T> Fail<T>(string message) => new()
    {
        Success = false,
        Message = message,
    };

    public static ApiResponse Fail(string message, bool includeInErrors) => new()
    {
        Success = false,
        Message = message,
        Errors = includeInErrors ? new List<string> { message } : new List<string>(),
    };

    public static ApiResponse<T> Fail<T>(string message, bool includeInErrors) => new()
    {
        Success = false,
        Message = message,
        Errors = includeInErrors ? new List<string> { message } : new List<string>(),
    };

    public static ApiResponse FailFromErrors(IEnumerable<string> errors, string fallbackMessage)
    {
        var errorList = errors.ToList();
        return new ApiResponse
        {
            Success = false,
            Message = errorList.FirstOrDefault() ?? fallbackMessage,
            Errors = errorList,
        };
    }

    public static ApiResponse<T> FailFromErrors<T>(IEnumerable<string> errors, string fallbackMessage)
    {
        var errorList = errors.ToList();
        return new ApiResponse<T>
        {
            Success = false,
            Message = errorList.FirstOrDefault() ?? fallbackMessage,
            Errors = errorList,
        };
    }

    public static IActionResult SoftDeleteResult(
        ControllerBase controller,
        bool ok,
        bool notFound,
        IEnumerable<string> errors,
        string notFoundMessage,
        string failedFallbackMessage,
        string successMessage)
    {
        if (notFound)
        {
            return controller.NotFound(Fail(notFoundMessage));
        }

        if (!ok)
        {
            return controller.BadRequest(FailFromErrors(errors, failedFallbackMessage));
        }

        return controller.Ok(new ApiResponse
        {
            Success = true,
            Message = successMessage,
        });
    }
}
