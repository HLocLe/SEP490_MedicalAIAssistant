using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlanRequests;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.IService;

public interface IRecoveryPlanRequestService
{
    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> CreateAsync(
        Guid userId,
        string idempotencyKey,
        CreateRecoveryPlanRequest request,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>> GetMineAsync(
        Guid userId,
        PaginationQuery page,
        RecoveryPlanRequestStatus? status,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> GetDetailAsync(
        Guid userId,
        bool isDoctor,
        bool isAdmin,
        Guid requestId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> CancelAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> ProvideInformationAsync(
        Guid userId,
        Guid requestId,
        string information,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<PagedResponse<OpenRecoveryPlanRequestResponse>>> GetOpenAsync(
        Guid doctorUserId,
        PaginationQuery page,
        RecoveryPlanDiseaseGroup? diseaseGroup,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<DoctorRecoveryPlanRequestResponse>> GetDoctorDetailAsync(
        Guid doctorUserId,
        Guid requestId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<PagedResponse<DoctorRecoveryPlanRequestResponse>>> GetDoctorMineAsync(
        Guid doctorUserId,
        PaginationQuery page,
        RecoveryPlanRequestStatus? status,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> AcceptAsync(
        Guid doctorUserId,
        Guid requestId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> StartReviewAsync(
        Guid doctorUserId,
        Guid requestId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> ReleaseAsync(
        Guid doctorUserId,
        Guid requestId,
        string? reason,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> RequestInformationAsync(
        Guid doctorUserId,
        Guid requestId,
        string reason,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> RejectAsync(
        Guid doctorUserId,
        Guid requestId,
        string code,
        string reason,
        CancellationToken cancellationToken);
}

public interface IRecoveryPlanQuotaService
{
    Task<RecoveryPlanOperationResult<IReadOnlyList<SubscriptionUsageResponse>>> GetCurrentUsageAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<RecoveryPlanOperationResult<MedMateAI.Domain.Entities.UserSubscriptionUsage>> ResolveUsageAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<QuotaMutationStatus> ReserveAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        Guid requestId,
        Guid actorUserId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<QuotaMutationStatus> ReleaseAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        Guid requestId,
        Guid? actorUserId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<QuotaMutationStatus> ConsumeAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        Guid requestId,
        Guid? actorUserId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<QuotaMutationStatus> RestoreAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        Guid requestId,
        Guid? actorUserId,
        string idempotencyKey,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
