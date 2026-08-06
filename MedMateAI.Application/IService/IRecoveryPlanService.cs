using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.IService;

public interface IRecoveryPlanService
{
    Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> CreateDraftAsync(
        Guid doctorUserId,
        Guid requestId,
        CreateRecoveryPlanDraftRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<DoctorRecoveryPlanDetailResponse>> GetDoctorDetailAsync(
        Guid doctorUserId,
        Guid planId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> UpdateDraftAsync(
        Guid doctorUserId,
        Guid planId,
        UpdateRecoveryPlanDraftRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<bool>> DeleteDraftAsync(
        Guid doctorUserId,
        Guid planId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>> CreatePhaseAsync(
        Guid doctorUserId,
        Guid planId,
        UpsertRecoveryPlanPhaseRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>> UpdatePhaseAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        UpsertRecoveryPlanPhaseRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<bool>> DeletePhaseAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>> CreateNutrientAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        UpsertRecoveryPlanNutrientTargetRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>> UpdateNutrientAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        UpsertRecoveryPlanNutrientTargetRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<bool>> DeleteNutrientAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>> CreateFoodAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        UpsertRecoveryPlanFoodSourceRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>> UpdateFoodAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        UpsertRecoveryPlanFoodSourceRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<bool>> DeleteFoodAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> PublishAsync(
        Guid doctorUserId,
        Guid planId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<PagedResponse<RecoveryPlanSummaryResponse>>> GetMineAsync(
        Guid userId,
        PaginationQuery page,
        RecoveryPlanStatus? status,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> GetUserDetailAsync(
        Guid userId,
        Guid planId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> StartAsync(
        Guid userId,
        Guid planId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> CancelAsync(
        Guid userId,
        Guid planId,
        CancelRecoveryPlanRequest request,
        CancellationToken cancellationToken);
}

public interface IRecoveryPlanClinicalContextService
{
    Task<RecoveryPlanOperationResult<RecoveryPlanClinicalContextResponse>> GetForDoctorAsync(
        Guid doctorUserId,
        Guid requestId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanClinicalSnapshot?> BuildSnapshotAsync(
        Guid requestId,
        DateTime capturedAtUtc,
        CancellationToken cancellationToken);

    string SerializeSnapshot(RecoveryPlanClinicalSnapshot snapshot);

    RecoveryPlanClinicalSnapshot? DeserializeSnapshot(string? snapshotJson);
}
