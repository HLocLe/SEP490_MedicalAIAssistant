using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlanRequests;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.IService;

public interface IRecoveryPlanRequestService
{
    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> CreateAsync(Guid userId, string idempotencyKey, CreateRecoveryPlanRequest request, CancellationToken token);
    Task<RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>> GetMineAsync(Guid userId, PaginationQuery page, RecoveryPlanRequestStatus? status, CancellationToken token);
    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> GetDetailAsync(Guid userId, bool isDoctor, bool isAdmin, Guid id, CancellationToken token);
    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> CancelAsync(Guid userId, Guid id, CancellationToken token);
    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> ProvideInformationAsync(Guid userId, Guid id, string information, CancellationToken token);
    Task<RecoveryPlanOperationResult<PagedResponse<OpenRecoveryPlanRequestResponse>>> GetOpenAsync(Guid doctorUserId, PaginationQuery page, RecoveryPlanDiseaseGroup? group, CancellationToken token);
    Task<RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>> GetDoctorMineAsync(Guid doctorUserId, PaginationQuery page, RecoveryPlanRequestStatus? status, CancellationToken token);
    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> AcceptAsync(Guid doctorUserId, Guid id, CancellationToken token);
    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> StartReviewAsync(Guid doctorUserId, Guid id, CancellationToken token);
    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> ReleaseAsync(Guid doctorUserId, Guid id, string? reason, CancellationToken token);
    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> RequestInformationAsync(Guid doctorUserId, Guid id, string reason, CancellationToken token);
    Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> RejectAsync(Guid doctorUserId, Guid id, string code, string reason, CancellationToken token);
}

public interface IRecoveryPlanQuotaService
{
    Task<RecoveryPlanOperationResult<IReadOnlyList<SubscriptionUsageResponse>>> GetCurrentUsageAsync(Guid userId, CancellationToken token);
    Task<RecoveryPlanOperationResult<MedMateAI.Domain.Entities.UserSubscriptionUsage>> ResolveUsageAsync(Guid userId, DateTime now, CancellationToken token);
    Task<bool> ReserveAsync(Guid usageId, Guid subscriptionId, Guid quotaId, Guid requestId, Guid actorId, string key, DateTime now, CancellationToken token);
    Task<bool> ReleaseAsync(Guid usageId, Guid subscriptionId, Guid quotaId, Guid requestId, Guid? actorId, string key, DateTime now, CancellationToken token);
    Task<bool> ConsumeAsync(Guid usageId, Guid subscriptionId, Guid quotaId, Guid requestId, Guid? actorId, string key, DateTime now, CancellationToken token);
    Task<bool> RestoreAsync(Guid usageId, Guid subscriptionId, Guid quotaId, Guid requestId, Guid? actorId, string key, DateTime now, CancellationToken token);
}
