using MedMateAI.Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MedMateAI.Controllers;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class SubscriptionPlanQuotaValidationFilterAttribute
    : ActionFilterAttribute
{
    public SubscriptionPlanQuotaValidationFilterAttribute()
    {
        Order = -3000;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
        {
            return;
        }

        context.Result = new BadRequestObjectResult(new ApiResponse
        {
            Success = false,
            Message = "Invalid subscription plan quota request.",
            Errors = new List<string> { "INVALID_REQUEST" },
        });
    }
}
