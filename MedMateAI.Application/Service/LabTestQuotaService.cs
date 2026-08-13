using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.ServiceCredits;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class LabTestQuotaService : ILabTestQuotaService
{
    private const string ReferenceType = "LabTestSession";
    private const string ReserveReason = "Lab test service credit reserved.";
    private const string ConsumeReason = "Lab test service credit consumed.";
    private const string ReleaseReason = "Lab test service credit released.";

    private readonly IServiceCreditService _serviceCreditService;
    private readonly IGenericRepository<LabTestSession> _sessions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LabTestQuotaService> _logger;

    public LabTestQuotaService(
        IServiceCreditService serviceCreditService,
        IGenericRepository<LabTestSession> sessions,
        IUnitOfWork unitOfWork,
        ILogger<LabTestQuotaService> logger)
    {
        _serviceCreditService = serviceCreditService;
        _sessions = sessions;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task<ServiceCreditOperationResult<UserSubscriptionUsage>> ReserveAsync(
        Guid userId,
        Guid sessionId,
        Guid actorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return _serviceCreditService.ReserveAsync(
            userId,
            ReferenceType,
            sessionId,
            actorUserId,
            $"labtest:reserve:{sessionId:N}",
            ReserveReason,
            utcNow,
            cancellationToken);
    }

    public async Task FinalizeAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.FirstOrDefaultAsync(
            current => current.Id == sessionId && !current.IsDeleted,
            cancellationToken: cancellationToken);

        if (session is null)
        {
            _logger.LogWarning(
                "Lab test quota finalization skipped because session {SessionId} was not found.",
                sessionId);
            return;
        }

        if (!session.UserSubscriptionId.HasValue
            && !session.UserSubscriptionUsageId.HasValue)
        {
            return;
        }

        if (!session.UserSubscriptionId.HasValue
            || !session.UserSubscriptionUsageId.HasValue)
        {
            _logger.LogError(
                "Lab test session {SessionId} has inconsistent service credit linkage.",
                sessionId);
            return;
        }

        var actionType = session.Status switch
        {
            LabTestSessionStatus.Completed => SubscriptionQuotaActionType.Consume,
            LabTestSessionStatus.Failed => SubscriptionQuotaActionType.Release,
            _ => (SubscriptionQuotaActionType?)null
        };

        if (!actionType.HasValue)
        {
            return;
        }

        await FinalizeMutationAsync(
            session,
            actionType.Value,
            cancellationToken);
    }

    private async Task FinalizeMutationAsync(
        LabTestSession session,
        SubscriptionQuotaActionType actionType,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var usage = await _unitOfWork.QuotaUsages.GetByIdAsync(
                session.UserSubscriptionUsageId!.Value,
                cancellationToken);
            if (usage is null
                || usage.UserSubscriptionId != session.UserSubscriptionId!.Value)
            {
                throw new InvalidOperationException(
                    "The lab test session service credit linkage is invalid.");
            }

            var isConsume = actionType == SubscriptionQuotaActionType.Consume;
            var mutationStatus = await _serviceCreditService.MutateAsync(
                usage.Id,
                usage.UserSubscriptionId,
                usage.QuotaId,
                actionType,
                ReferenceType,
                session.Id,
                session.UserId,
                isConsume
                    ? $"labtest:consume:{session.Id:N}"
                    : $"labtest:release:{session.Id:N}",
                isConsume ? ConsumeReason : ReleaseReason,
                DateTime.UtcNow,
                cancellationToken);

            if (mutationStatus == QuotaMutationStatus.Rejected)
            {
                throw new InvalidOperationException(
                    "The lab test session service credit mutation was rejected.");
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            _logger.LogError(
                ex,
                "Lab test quota finalization failed for session {SessionId} and action {ActionType}.",
                session.Id,
                actionType);
            throw;
        }
    }
}
