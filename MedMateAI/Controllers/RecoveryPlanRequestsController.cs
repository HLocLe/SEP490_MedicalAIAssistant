using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlanRequests;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize]
[Route("api/recovery-plan-requests")]
public sealed class RecoveryPlanRequestsController : ControllerBase
{
    private readonly IRecoveryPlanRequestService _service;

    public RecoveryPlanRequestsController(IRecoveryPlanRequestService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CreateRecoveryPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await _service.CreateAsync(
            userId,
            idempotencyKey ?? string.Empty,
            request,
            cancellationToken);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        var response = new ApiResponse<RecoveryPlanRequestResponse>
        {
            Success = true,
            Message = result.IsReplay ? "Idempotent replay." : "Recovery plan request created.",
            Data = result.Data
        };

        return result.IsReplay ? Ok(response) : CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, response);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(
        [FromQuery] PaginationQuery page,
        [FromQuery] RecoveryPlanRequestStatus? status,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await _service.GetMineAsync(
            userId,
            page,
            status,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        [FromRoute(Name = "id")] Guid requestId,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await _service.GetDetailAsync(
            userId,
            User.IsInRole("Doctor"),
            User.IsInRole("Admin"),
            requestId,
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        [FromRoute(Name = "id")] Guid requestId,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await _service.CancelAsync(userId, requestId, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/provide-more-information")]
    public async Task<IActionResult> ProvideInformation(
        [FromRoute(Name = "id")] Guid requestId,
        [FromBody] ProvideMoreInformationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await _service.ProvideInformationAsync(
            userId,
            requestId,
            request.AdditionalInformation,
            cancellationToken);

        return this.ToActionResult(result);
    }

    private bool TryUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
