using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans.Analytics;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize(Roles = "Doctor")]
[Route("api/doctor/recovery-plans/analytics")]
public sealed class DoctorRecoveryPlanAnalyticsController : ControllerBase
{
    private readonly IRecoveryPlanFeedbackAnalyticsService _service;

    public DoctorRecoveryPlanAnalyticsController(
        IRecoveryPlanFeedbackAnalyticsService service)
    {
        _service = service;
    }

    [HttpGet("feedback")]
    [ProducesResponseType(
        typeof(ApiResponse<RecoveryPlanFeedbackAnalyticsResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFeedbackAnalytics(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var doctorUserId))
        {
            return this.AnalyticsUnauthorized();
        }

        var result = await _service.GetAsync(
            doctorUserId,
            from,
            to,
            cancellationToken);
        return this.ToAnalyticsActionResult(result);
    }
}
