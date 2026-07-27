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
    public RecoveryPlanRequestsController(IRecoveryPlanRequestService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create([FromHeader(Name = "Idempotency-Key")] string? key,
        [FromBody] CreateRecoveryPlanRequest request, CancellationToken token)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await _service.CreateAsync(userId, key ?? string.Empty, request, token);
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
    public async Task<IActionResult> GetMine([FromQuery] PaginationQuery page,
        [FromQuery] RecoveryPlanRequestStatus? status, CancellationToken token)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        return this.ToActionResult(await _service.GetMineAsync(userId, page, status, token));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken token)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        return this.ToActionResult(
            await _service.GetDetailAsync(userId, User.IsInRole("Doctor"), User.IsInRole("Admin"), id, token));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken token)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        return this.ToActionResult(await _service.CancelAsync(userId, id, token));
    }

    [HttpPost("{id:guid}/provide-more-information")]
    public async Task<IActionResult> ProvideInformation(Guid id, [FromBody] ProvideMoreInformationRequest request, CancellationToken token)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        return this.ToActionResult(
            await _service.ProvideInformationAsync(userId, id, request.AdditionalInformation, token));
    }

    private bool TryUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
