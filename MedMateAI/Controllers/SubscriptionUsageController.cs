using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.UserSubscriptions.Responses;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize]
[Route("api/me/subscription-usage")]
public sealed class SubscriptionUsageController : ControllerBase
{
    private readonly IRecoveryPlanQuotaService _service;
    public SubscriptionUsageController(IRecoveryPlanQuotaService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(new ApiResponse { Success = false, Message = "Unauthorized.", Errors = new() { "UNAUTHENTICATED" } });
        var result = await _service.GetCurrentUsageAsync(userId, token);
        if (result.Success)
            return Ok(new ApiResponse<IReadOnlyList<SubscriptionUsageResponse>> { Success = true, Message = "OK", Data = result.Data });
        var code = result.Error == Application.Models.RecoveryPlanErrorCode.NoActiveSubscription
            ? "NO_ACTIVE_SUBSCRIPTION" : result.Error.ToString().ToUpperInvariant();
        return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse
            { Success = false, Message = "Subscription usage unavailable.", Errors = new() { code } });
    }
}
