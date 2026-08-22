using System.Security.Claims;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.DTOs.RecoveryPlanTemplates;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedMateAI.Controllers;

[ApiController, Authorize(Roles = "Doctor")]
[Route("api/doctor/recovery-plan-templates")]
public sealed class DoctorRecoveryPlanTemplatesController : ControllerBase
{
    private readonly IRecoveryPlanTemplateService _service;

    public DoctorRecoveryPlanTemplatesController(IRecoveryPlanTemplateService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] PaginationQuery page,
        [FromQuery] RecoveryPlanDiseaseGroup? diseaseGroup,
        [FromQuery] string? search,
        CancellationToken cancellationToken) =>
        await WithUser(userId => _service.GetPagedAsync(
            userId,
            page,
            diseaseGroup,
            search,
            cancellationToken));

    [HttpGet("{templateId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid templateId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.GetDetailAsync(userId, templateId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecoveryPlanTemplateRequest request,
        CancellationToken cancellationToken) =>
        await WithUserCreated(
            userId => _service.CreateAsync(userId, request, cancellationToken),
            "Recovery plan template created.");

    [HttpPut("{templateId:guid}")]
    public async Task<IActionResult> Update(
        Guid templateId,
        [FromBody] UpdateRecoveryPlanTemplateRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.UpdateAsync(userId, templateId, request, cancellationToken));

    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> Delete(
        Guid templateId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.DeleteAsync(userId, templateId, cancellationToken));

    [HttpPost("{templateId:guid}/phases")]
    public async Task<IActionResult> CreatePhase(
        Guid templateId,
        [FromBody] UpsertRecoveryPlanPhaseRequest request,
        CancellationToken cancellationToken) =>
        await WithUserCreated(
            userId => _service.CreatePhaseAsync(
                userId,
                templateId,
                request,
                cancellationToken),
            "Recovery plan template phase created.");

    [HttpPut("{templateId:guid}/phases/{phaseId:guid}")]
    public async Task<IActionResult> UpdatePhase(
        Guid templateId,
        Guid phaseId,
        [FromBody] UpsertRecoveryPlanPhaseRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId => _service.UpdatePhaseAsync(
            userId,
            templateId,
            phaseId,
            request,
            cancellationToken));

    [HttpDelete("{templateId:guid}/phases/{phaseId:guid}")]
    public async Task<IActionResult> DeletePhase(
        Guid templateId,
        Guid phaseId,
        CancellationToken cancellationToken) =>
        await WithUser(userId =>
            _service.DeletePhaseAsync(userId, templateId, phaseId, cancellationToken));

    [HttpPost("{templateId:guid}/phases/{phaseId:guid}/nutrients")]
    public async Task<IActionResult> CreateNutrient(
        Guid templateId,
        Guid phaseId,
        [FromBody] UpsertRecoveryPlanNutrientTargetRequest request,
        CancellationToken cancellationToken) =>
        await WithUserCreated(
            userId => _service.CreateNutrientAsync(
                userId,
                templateId,
                phaseId,
                request,
                cancellationToken),
            "Recovery plan template nutrient target created.");

    [HttpPut("{templateId:guid}/phases/{phaseId:guid}/nutrients/{nutrientId:guid}")]
    public async Task<IActionResult> UpdateNutrient(
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        [FromBody] UpsertRecoveryPlanNutrientTargetRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId => _service.UpdateNutrientAsync(
            userId,
            templateId,
            phaseId,
            nutrientId,
            request,
            cancellationToken));

    [HttpDelete("{templateId:guid}/phases/{phaseId:guid}/nutrients/{nutrientId:guid}")]
    public async Task<IActionResult> DeleteNutrient(
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        CancellationToken cancellationToken) =>
        await WithUser(userId => _service.DeleteNutrientAsync(
            userId,
            templateId,
            phaseId,
            nutrientId,
            cancellationToken));

    [HttpPost(
        "{templateId:guid}/phases/{phaseId:guid}/nutrients/{nutrientId:guid}/foods")]
    public async Task<IActionResult> CreateFood(
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        [FromBody] UpsertRecoveryPlanFoodSourceRequest request,
        CancellationToken cancellationToken) =>
        await WithUserCreated(
            userId => _service.CreateFoodAsync(
                userId,
                templateId,
                phaseId,
                nutrientId,
                request,
                cancellationToken),
            "Recovery plan template food source created.");

    [HttpPut(
        "{templateId:guid}/phases/{phaseId:guid}/nutrients/{nutrientId:guid}/foods/{foodSourceId:guid}")]
    public async Task<IActionResult> UpdateFood(
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        [FromBody] UpsertRecoveryPlanFoodSourceRequest request,
        CancellationToken cancellationToken) =>
        await WithUser(userId => _service.UpdateFoodAsync(
            userId,
            templateId,
            phaseId,
            nutrientId,
            foodSourceId,
            request,
            cancellationToken));

    [HttpDelete(
        "{templateId:guid}/phases/{phaseId:guid}/nutrients/{nutrientId:guid}/foods/{foodSourceId:guid}")]
    public async Task<IActionResult> DeleteFood(
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        CancellationToken cancellationToken) =>
        await WithUser(userId => _service.DeleteFoodAsync(
            userId,
            templateId,
            phaseId,
            nutrientId,
            foodSourceId,
            cancellationToken));

    private async Task<IActionResult> WithUser<T>(
        Func<Guid, Task<RecoveryPlanOperationResult<T>>> action)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        return this.ToActionResult(await action(userId));
    }

    private async Task<IActionResult> WithUserCreated<T>(
        Func<Guid, Task<RecoveryPlanOperationResult<T>>> action,
        string message)
    {
        if (!TryUserId(out var userId))
        {
            return this.UnauthorizedResult();
        }

        return this.ToCreatedResult(await action(userId), message);
    }

    private bool TryUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
