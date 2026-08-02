using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.DTOs.RecoveryPlanRequests;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize(Roles = "Doctor")]
[Route("api/doctor/recovery-plan-requests")]
public sealed class DoctorRecoveryPlanRequestsController : ControllerBase
{
    private readonly IRecoveryPlanRequestService _service;
    private readonly IRecoveryPlanService _planService;
    private readonly IRecoveryPlanClinicalContextService _clinicalContextService;

    public DoctorRecoveryPlanRequestsController(
        IRecoveryPlanRequestService service,
        IRecoveryPlanService planService,
        IRecoveryPlanClinicalContextService clinicalContextService)
    {
        _service = service;
        _planService = planService;
        _clinicalContextService = clinicalContextService;
    }

    [HttpGet("open")]
    public async Task<IActionResult> Open(
        [FromQuery] PaginationQuery page,
        [FromQuery] RecoveryPlanDiseaseGroup? diseaseGroup,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.GetOpenAsync(userId, page, diseaseGroup, cancellationToken));

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(
        [FromQuery] PaginationQuery page,
        [FromQuery] RecoveryPlanRequestStatus? status,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.GetDoctorMineAsync(userId, page, status, cancellationToken));

    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> Detail(
        Guid requestId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.GetDoctorDetailAsync(userId, requestId, cancellationToken));

    [HttpGet("{requestId:guid}/clinical-context")]
    public async Task<IActionResult> ClinicalContext(
        Guid requestId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _clinicalContextService.GetForDoctorAsync(
                userId,
                requestId,
                cancellationToken));

    [HttpPost("{requestId:guid}/plan")]
    public async Task<IActionResult> CreatePlan(
        Guid requestId,
        [FromBody] CreateRecoveryPlanDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await _planService.CreateDraftAsync(
            userId,
            requestId,
            request,
            cancellationToken);

        return this.ToCreatedResult(result, "Recovery plan draft created.");
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(
        [FromRoute(Name = "id")] Guid requestId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.AcceptAsync(userId, requestId, cancellationToken));

    [HttpPost("{id:guid}/start-review")]
    public async Task<IActionResult> StartReview(
        [FromRoute(Name = "id")] Guid requestId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.StartReviewAsync(userId, requestId, cancellationToken));

    [HttpPost("{id:guid}/release")]
    public async Task<IActionResult> Release(
        [FromRoute(Name = "id")] Guid requestId,
        [FromBody] ReleaseRecoveryPlanRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.ReleaseAsync(userId, requestId, request.Reason, cancellationToken));

    [HttpPost("{id:guid}/request-more-information")]
    public async Task<IActionResult> RequestInformation(
        [FromRoute(Name = "id")] Guid requestId,
        [FromBody] RequestMoreInformationRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.RequestInformationAsync(userId, requestId, request.Reason, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        [FromRoute(Name = "id")] Guid requestId,
        [FromBody] RejectRecoveryPlanRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.RejectAsync(
                userId,
                requestId,
                request.RejectionReasonCode,
                request.RejectionReason,
                cancellationToken));

    private async Task<IActionResult> WithUser<T>(Func<Guid, Task<RecoveryPlanOperationResult<T>>> action)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await action(userId);
        return this.ToActionResult(result);
    }

    private bool TryUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
