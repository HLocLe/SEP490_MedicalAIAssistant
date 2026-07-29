using System.Security.Claims;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize(Roles = "Doctor")]
[Route("api/doctor/recovery-plans")]
public sealed class DoctorRecoveryPlansController : ControllerBase
{
    private readonly IRecoveryPlanService _service;

    public DoctorRecoveryPlansController(IRecoveryPlanService service)
    {
        _service = service;
    }

    [HttpGet("{planId:guid}")]
    public async Task<IActionResult> GetById(
        Guid planId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.GetDoctorDetailAsync(userId, planId, cancellationToken));

    [HttpPut("{planId:guid}")]
    public async Task<IActionResult> Update(
        Guid planId,
        [FromBody] UpdateRecoveryPlanDraftRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.UpdateDraftAsync(userId, planId, request, cancellationToken));

    [HttpDelete("{planId:guid}")]
    public async Task<IActionResult> Delete(
        Guid planId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.DeleteDraftAsync(userId, planId, cancellationToken));

    [HttpPost("{planId:guid}/phases")]
    public async Task<IActionResult> CreatePhase(
        Guid planId,
        [FromBody] UpsertRecoveryPlanPhaseRequest request,
        CancellationToken cancellationToken) =>
        await WithUserCreated(
            userId => _service.CreatePhaseAsync(
                userId,
                planId,
                request,
                cancellationToken),
            "Recovery plan phase created.");

    [HttpPut("{planId:guid}/phases/{phaseId:guid}")]
    public async Task<IActionResult> UpdatePhase(
        Guid planId,
        Guid phaseId,
        [FromBody] UpsertRecoveryPlanPhaseRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.UpdatePhaseAsync(
                userId,
                planId,
                phaseId,
                request,
                cancellationToken));

    [HttpDelete("{planId:guid}/phases/{phaseId:guid}")]
    public async Task<IActionResult> DeletePhase(
        Guid planId,
        Guid phaseId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.DeletePhaseAsync(userId, planId, phaseId, cancellationToken));

    [HttpPost("{planId:guid}/phases/{phaseId:guid}/nutrients")]
    public async Task<IActionResult> CreateNutrient(
        Guid planId,
        Guid phaseId,
        [FromBody] UpsertRecoveryPlanNutrientTargetRequest request,
        CancellationToken cancellationToken) =>
        await WithUserCreated(
            userId => _service.CreateNutrientAsync(
                userId,
                planId,
                phaseId,
                request,
                cancellationToken),
            "Recovery plan nutrient target created.");

    [HttpPut("{planId:guid}/phases/{phaseId:guid}/nutrients/{nutrientId:guid}")]
    public async Task<IActionResult> UpdateNutrient(
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        [FromBody] UpsertRecoveryPlanNutrientTargetRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.UpdateNutrientAsync(
                userId,
                planId,
                phaseId,
                nutrientId,
                request,
                cancellationToken));

    [HttpDelete("{planId:guid}/phases/{phaseId:guid}/nutrients/{nutrientId:guid}")]
    public async Task<IActionResult> DeleteNutrient(
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.DeleteNutrientAsync(
                userId,
                planId,
                phaseId,
                nutrientId,
                cancellationToken));

    [HttpPost("{planId:guid}/phases/{phaseId:guid}/nutrients/{nutrientId:guid}/foods")]
    public async Task<IActionResult> CreateFood(
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        [FromBody] UpsertRecoveryPlanFoodSourceRequest request,
        CancellationToken cancellationToken) =>
        await WithUserCreated(
            userId => _service.CreateFoodAsync(
                userId,
                planId,
                phaseId,
                nutrientId,
                request,
                cancellationToken),
            "Recovery plan food source created.");

    [HttpPut(
        "{planId:guid}/phases/{phaseId:guid}/nutrients/{nutrientId:guid}/foods/{foodSourceId:guid}")]
    public async Task<IActionResult> UpdateFood(
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        [FromBody] UpsertRecoveryPlanFoodSourceRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.UpdateFoodAsync(
                userId,
                planId,
                phaseId,
                nutrientId,
                foodSourceId,
                request,
                cancellationToken));

    [HttpDelete(
        "{planId:guid}/phases/{phaseId:guid}/nutrients/{nutrientId:guid}/foods/{foodSourceId:guid}")]
    public async Task<IActionResult> DeleteFood(
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.DeleteFoodAsync(
                userId,
                planId,
                phaseId,
                nutrientId,
                foodSourceId,
                cancellationToken));

    [HttpPost("{planId:guid}/publish")]
    public async Task<IActionResult> Publish(
        Guid planId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.PublishAsync(userId, planId, cancellationToken));

    private async Task<IActionResult> WithUser<T>(
        Func<Guid, Task<RecoveryPlanOperationResult<T>>> action)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await action(userId);
        return this.ToActionResult(result);
    }

    private async Task<IActionResult> WithUserCreated<T>(
        Func<Guid, Task<RecoveryPlanOperationResult<T>>> action,
        string createdMessage)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        var result = await action(userId);
        return this.ToCreatedResult(result, createdMessage);
    }

    private bool TryUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
