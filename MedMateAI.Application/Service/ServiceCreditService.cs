using MedMateAI.Application.DTOs.UserSubscriptions.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.ServiceCredits;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;

namespace MedMateAI.Application.Service;

public sealed class ServiceCreditService : IServiceCreditService
{
    private const string PaymentReferenceType = "Payment";
    private const string GrantReason = "Purchased service credits granted.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubscriptionPlanQuotaRepository _subscriptionPlanQuotaRepository;

    public ServiceCreditService(
        IUnitOfWork unitOfWork,
        ISubscriptionPlanQuotaRepository subscriptionPlanQuotaRepository)
    {
        _unitOfWork = unitOfWork;
        _subscriptionPlanQuotaRepository = subscriptionPlanQuotaRepository;
    }

    public async Task<ServiceCreditOperationResult<SubscriptionUsageResponse>> GetBalanceAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var usages = await _unitOfWork.QuotaUsages.GetEligibleByUserAsync(
            userId,
            IServiceCreditService.QuotaCode,
            utcNow,
            cancellationToken);

        var response = new SubscriptionUsageResponse
        {
            QuotaCode = IServiceCreditService.QuotaCode,
            GrantedCount = usages.Sum(usage => usage.LimitValue),
            UsedCount = usages.Sum(usage => usage.UsedCount),
            ReservedCount = usages.Sum(usage => usage.ReservedCount)
        };

        return ServiceCreditOperationResult<SubscriptionUsageResponse>.Ok(response);
    }

    public async Task<ServiceCreditOperationResult<UserSubscriptionUsage>> ReserveAsync(
        Guid userId,
        string referenceType,
        Guid referenceId,
        Guid? actorUserId,
        string idempotencyKey,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.QuotaUsages.AcquireIdempotencyLockAsync(
            idempotencyKey,
            cancellationToken);

        var existingLog = await _unitOfWork.QuotaUsages.GetLogByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);
        if (existingLog is not null)
        {
            var replayUsage = await _unitOfWork.QuotaUsages.GetByIdAsync(
                existingLog.UserSubscriptionUsageId,
                cancellationToken);
            return replayUsage is null
                ? ServiceCreditOperationResult<UserSubscriptionUsage>.Fail(
                    ServiceCreditErrorCode.QuotaMutationFailed)
                : ServiceCreditOperationResult<UserSubscriptionUsage>.Ok(
                    replayUsage,
                    isReplay: true);
        }

        var quotaDefinition = await _subscriptionPlanQuotaRepository
            .GetQuotaDefinitionByCodeAsync(
                IServiceCreditService.QuotaCode,
                cancellationToken);
        if (quotaDefinition is null || !quotaDefinition.IsActive)
        {
            return ServiceCreditOperationResult<UserSubscriptionUsage>.Fail(
                ServiceCreditErrorCode.ServiceCreditNotConfigured);
        }

        var candidates = await _unitOfWork.QuotaUsages.GetEligibleByUserAsync(
            userId,
            IServiceCreditService.QuotaCode,
            utcNow,
            cancellationToken);
        if (candidates.Count == 0)
        {
            return ServiceCreditOperationResult<UserSubscriptionUsage>.Fail(
                ServiceCreditErrorCode.NoCreditPackage);
        }

        var availableCandidates = candidates
            .Where(candidate =>
                candidate.UsedCount + candidate.ReservedCount < candidate.LimitValue)
            .ToList();
        if (availableCandidates.Count == 0)
        {
            return ServiceCreditOperationResult<UserSubscriptionUsage>.Fail(
                ServiceCreditErrorCode.ServiceCreditExhausted);
        }

        foreach (var candidate in availableCandidates)
        {
            var mutation = await _unitOfWork.QuotaUsages.ReserveAsync(
                candidate.Id,
                utcNow,
                cancellationToken);
            if (mutation is null)
            {
                continue;
            }

            var log = CreateLog(
                mutation,
                SubscriptionQuotaActionType.Reserve,
                referenceType,
                referenceId,
                actorUserId,
                idempotencyKey,
                reason,
                quantity: 1,
                utcNow);
            var inserted = await _unitOfWork.QuotaUsages.TryInsertLogAsync(
                log,
                cancellationToken);

            return ServiceCreditOperationResult<UserSubscriptionUsage>.Ok(
                candidate,
                isReplay: !inserted);
        }

        var refreshedCandidates = await _unitOfWork.QuotaUsages.GetEligibleByUserAsync(
            userId,
            IServiceCreditService.QuotaCode,
            utcNow,
            cancellationToken);
        if (refreshedCandidates.Count == 0)
        {
            return ServiceCreditOperationResult<UserSubscriptionUsage>.Fail(
                ServiceCreditErrorCode.QuotaMutationFailed);
        }

        var hasRemainingCredit = refreshedCandidates.Any(candidate =>
            candidate.UsedCount + candidate.ReservedCount < candidate.LimitValue);

        return ServiceCreditOperationResult<UserSubscriptionUsage>.Fail(
            hasRemainingCredit
                ? ServiceCreditErrorCode.QuotaMutationFailed
                : ServiceCreditErrorCode.ServiceCreditExhausted);
    }

    public async Task<QuotaMutationStatus> MutateAsync(
        Guid usageId,
        Guid userSubscriptionId,
        Guid quotaId,
        SubscriptionQuotaActionType actionType,
        string referenceType,
        Guid referenceId,
        Guid? actorUserId,
        string idempotencyKey,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.QuotaUsages.AcquireIdempotencyLockAsync(
            idempotencyKey,
            cancellationToken);

        var existingLog = await _unitOfWork.QuotaUsages.GetLogByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);
        if (existingLog is not null)
        {
            return QuotaMutationStatus.Duplicate;
        }

        var usage = await _unitOfWork.QuotaUsages.GetByIdForQuotaAsync(
            usageId,
            userSubscriptionId,
            IServiceCreditService.QuotaCode,
            cancellationToken);
        if (usage is null || usage.QuotaId != quotaId)
        {
            return QuotaMutationStatus.Rejected;
        }

        var mutation = actionType switch
        {
            SubscriptionQuotaActionType.Release =>
                await _unitOfWork.QuotaUsages.ReleaseAsync(usageId, utcNow, cancellationToken),
            SubscriptionQuotaActionType.Consume =>
                await _unitOfWork.QuotaUsages.ConsumeAsync(usageId, utcNow, cancellationToken),
            SubscriptionQuotaActionType.Restore =>
                await _unitOfWork.QuotaUsages.RestoreAsync(usageId, utcNow, cancellationToken),
            _ => null
        };
        if (mutation is null)
        {
            return QuotaMutationStatus.Rejected;
        }

        var log = CreateLog(
            mutation,
            actionType,
            referenceType,
            referenceId,
            actorUserId,
            idempotencyKey,
            reason,
            quantity: 1,
            utcNow);
        var inserted = await _unitOfWork.QuotaUsages.TryInsertLogAsync(log, cancellationToken);
        return inserted ? QuotaMutationStatus.Applied : QuotaMutationStatus.Duplicate;
    }

    public async Task<QuotaMutationStatus> GrantAsync(
        Guid userSubscriptionId,
        Guid paymentId,
        Guid actorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = $"credit:grant:payment:{paymentId:N}";
        var existingLog = await _unitOfWork.QuotaUsages.GetLogByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);
        if (existingLog is not null)
        {
            return QuotaMutationStatus.Duplicate;
        }

        var usage = await _unitOfWork.QuotaUsages.GetBySubscriptionForQuotaAsync(
            userSubscriptionId,
            IServiceCreditService.QuotaCode,
            cancellationToken);
        if (usage is null || usage.LimitValue <= 0)
        {
            return QuotaMutationStatus.Rejected;
        }

        var log = new UserSubscriptionLog
        {
            Id = Guid.NewGuid(),
            UserSubscriptionId = usage.UserSubscriptionId,
            UserSubscriptionUsageId = usage.Id,
            QuotaId = usage.QuotaId,
            ActionType = SubscriptionQuotaActionType.Grant,
            Quantity = usage.LimitValue,
            UsedCountBefore = usage.UsedCount,
            UsedCountAfter = usage.UsedCount,
            ReservedCountBefore = usage.ReservedCount,
            ReservedCountAfter = usage.ReservedCount,
            ReferenceType = PaymentReferenceType,
            ReferenceId = paymentId,
            Reason = GrantReason,
            IdempotencyKey = idempotencyKey,
            PerformedByUserId = actorUserId,
            CreatedAt = utcNow
        };

        var inserted = await _unitOfWork.QuotaUsages.TryInsertLogAsync(log, cancellationToken);
        return inserted ? QuotaMutationStatus.Applied : QuotaMutationStatus.Duplicate;
    }

    private static UserSubscriptionLog CreateLog(
        QuotaMutationResult mutation,
        SubscriptionQuotaActionType actionType,
        string referenceType,
        Guid referenceId,
        Guid? actorUserId,
        string idempotencyKey,
        string reason,
        int quantity,
        DateTime utcNow)
    {
        return new UserSubscriptionLog
        {
            Id = Guid.NewGuid(),
            UserSubscriptionId = mutation.UserSubscriptionId,
            UserSubscriptionUsageId = mutation.UsageId,
            QuotaId = mutation.QuotaId,
            ActionType = actionType,
            Quantity = quantity,
            UsedCountBefore = mutation.UsedCountBefore,
            UsedCountAfter = mutation.UsedCountAfter,
            ReservedCountBefore = mutation.ReservedCountBefore,
            ReservedCountAfter = mutation.ReservedCountAfter,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Reason = reason,
            IdempotencyKey = idempotencyKey,
            PerformedByUserId = actorUserId,
            CreatedAt = utcNow
        };
    }
}
