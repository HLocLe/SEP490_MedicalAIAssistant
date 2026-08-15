using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize]
[Route("api/recovery-plans")]
public sealed class RecoveryPlansController : ControllerBase
{
    private readonly IRecoveryPlanService _service;

    public RecoveryPlansController(IRecoveryPlanService service)
    {
        _service = service;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(
        [FromQuery] PaginationQuery page,
        [FromQuery] RecoveryPlanStatus? status,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.GetMineAsync(userId, page, status, cancellationToken));

    [HttpGet("{planId:guid}")]
    public async Task<IActionResult> GetById(
        Guid planId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.GetUserDetailAsync(userId, planId, cancellationToken));

    [HttpPost("{planId:guid}/start")]
    public async Task<IActionResult> Start(
        Guid planId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.StartAsync(userId, planId, cancellationToken));

    [HttpPost("{planId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid planId,
        [FromBody] CancelRecoveryPlanRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.CancelAsync(userId, planId, request, cancellationToken));

    [HttpPost("{planId:guid}/feedback")]
    public async Task<IActionResult> SubmitFeedback(
        Guid planId,
        [FromBody] SubmitRecoveryPlanFeedbackRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.SubmitFeedbackAsync(userId, planId, request, cancellationToken));

    private async Task<IActionResult> WithUser<T>(
        Func<Guid, Task<RecoveryPlanOperationResult<T>>> action)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await action(userId);
        return this.ToActionResult(result);
    }
}
