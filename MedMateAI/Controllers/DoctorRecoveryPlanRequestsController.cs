using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
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
    public DoctorRecoveryPlanRequestsController(IRecoveryPlanRequestService service) => _service = service;

    [HttpGet("open")]
    public async Task<IActionResult> Open([FromQuery] PaginationQuery page, [FromQuery] RecoveryPlanDiseaseGroup? diseaseGroup, CancellationToken token) =>
        await WithUser(id => _service.GetOpenAsync(id, page, diseaseGroup, token));
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] PaginationQuery page, [FromQuery] RecoveryPlanRequestStatus? status, CancellationToken token) =>
        await WithUser(id => _service.GetDoctorMineAsync(id, page, status, token));
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken token) => await WithUser(userId => _service.AcceptAsync(userId, id, token));
    [HttpPost("{id:guid}/start-review")]
    public async Task<IActionResult> StartReview(Guid id, CancellationToken token) => await WithUser(userId => _service.StartReviewAsync(userId, id, token));
    [HttpPost("{id:guid}/release")]
    public async Task<IActionResult> Release(Guid id, [FromBody] ReleaseRecoveryPlanRequest request, CancellationToken token) =>
        await WithUser(userId => _service.ReleaseAsync(userId, id, request.Reason, token));
    [HttpPost("{id:guid}/request-more-information")]
    public async Task<IActionResult> RequestInformation(Guid id, [FromBody] RequestMoreInformationRequest request, CancellationToken token) =>
        await WithUser(userId => _service.RequestInformationAsync(userId, id, request.Reason, token));
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRecoveryPlanRequest request, CancellationToken token) =>
        await WithUser(userId => _service.RejectAsync(userId, id, request.RejectionReasonCode, request.RejectionReason, token));

    private async Task<IActionResult> WithUser<T>(Func<Guid, Task<RecoveryPlanOperationResult<T>>> action)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await action(userId);
        return this.ToActionResult(result);
    }
}
