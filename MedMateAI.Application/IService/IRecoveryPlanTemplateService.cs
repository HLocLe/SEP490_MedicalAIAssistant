using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.DTOs.RecoveryPlanTemplates;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.IService;

public interface IRecoveryPlanTemplateService
{
    Task<RecoveryPlanOperationResult<PagedResponse<RecoveryPlanTemplateSummaryResponse>>>
        GetPagedAsync(
            Guid doctorUserId,
            PaginationQuery page,
            RecoveryPlanDiseaseGroup? diseaseGroup,
            string? search,
            CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>> GetDetailAsync(
        Guid doctorUserId,
        Guid templateId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>> CreateAsync(
        Guid doctorUserId,
        CreateRecoveryPlanTemplateRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>> UpdateAsync(
        Guid doctorUserId,
        Guid templateId,
        UpdateRecoveryPlanTemplateRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<bool>> DeleteAsync(
        Guid doctorUserId,
        Guid templateId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanTemplatePhaseResponse>> CreatePhaseAsync(
        Guid doctorUserId,
        Guid templateId,
        UpsertRecoveryPlanPhaseRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanTemplatePhaseResponse>> UpdatePhaseAsync(
        Guid doctorUserId,
        Guid templateId,
        Guid phaseId,
        UpsertRecoveryPlanPhaseRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<bool>> DeletePhaseAsync(
        Guid doctorUserId,
        Guid templateId,
        Guid phaseId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTargetResponse>>
        CreateNutrientAsync(
            Guid doctorUserId,
            Guid templateId,
            Guid phaseId,
            UpsertRecoveryPlanNutrientTargetRequest request,
            CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTargetResponse>>
        UpdateNutrientAsync(
            Guid doctorUserId,
            Guid templateId,
            Guid phaseId,
            Guid nutrientId,
            UpsertRecoveryPlanNutrientTargetRequest request,
            CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<bool>> DeleteNutrientAsync(
        Guid doctorUserId,
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSourceResponse>> CreateFoodAsync(
        Guid doctorUserId,
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        UpsertRecoveryPlanFoodSourceRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSourceResponse>> UpdateFoodAsync(
        Guid doctorUserId,
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        UpsertRecoveryPlanFoodSourceRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<bool>> DeleteFoodAsync(
        Guid doctorUserId,
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        CancellationToken cancellationToken);
}
