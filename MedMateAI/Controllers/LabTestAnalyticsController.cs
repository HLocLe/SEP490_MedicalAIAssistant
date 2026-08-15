using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.LabTests.Analytics;
using MedMateAI.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize]
[Route("api/lab-tests/analytics")]
public sealed class LabTestAnalyticsController : ControllerBase
{
    private readonly ILabTestAnalyticsService _service;

    public LabTestAnalyticsController(ILabTestAnalyticsService service)
    {
        _service = service;
    }

    [HttpGet("indicators")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<LabTestTrendIndicatorResponse>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetIndicators(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return this.AnalyticsUnauthorized();
        }

        var result = await _service.GetAvailableIndicatorsAsync(
            userId,
            from,
            to,
            cancellationToken);
        return this.ToAnalyticsActionResult(result);
    }

    [HttpGet("indicators/{indicatorId:guid}/trend")]
    [ProducesResponseType(
        typeof(ApiResponse<LabTestIndicatorTrendResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIndicatorTrend(
        Guid indicatorId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return this.AnalyticsUnauthorized();
        }

        var result = await _service.GetIndicatorTrendAsync(
            userId,
            indicatorId,
            from,
            to,
            cancellationToken);
        return this.ToAnalyticsActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
