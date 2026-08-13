using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.ServiceCredits;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class ConsultationSessionQuotaService : IConsultationSessionQuotaService
{
    private const string ReferenceType = "ConsultationSession";
    private const string ReserveReason = "Consultation session service credit reserved.";
    private const string ConsumeReason = "Consultation session service credit consumed.";
    private const string ReleaseReason = "Consultation session service credit released.";

    private readonly IServiceCreditService _serviceCreditService;
    private readonly IGenericRepository<ConsultationSession> _sessions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConsultationSessionQuotaService> _logger;

    public ConsultationSessionQuotaService(
        IServiceCreditService serviceCreditService,
        IGenericRepository<ConsultationSession> sessions,
        IUnitOfWork unitOfWork,
        ILogger<ConsultationSessionQuotaService> logger)
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
            $"consultation-session:reserve:{sessionId:N}",
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
                "Consultation quota finalization skipped because session {SessionId} was not found.",
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
                "Consultation session {SessionId} has inconsistent service credit linkage.",
                sessionId);
            return;
        }

        var actionType = session.Status switch
        {
            ConsultationSessionStatus.Completed => SubscriptionQuotaActionType.Consume,
            ConsultationSessionStatus.Failed => SubscriptionQuotaActionType.Release,
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
        ConsultationSession session,
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
                    "The consultation session service credit linkage is invalid.");
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
                    ? $"consultation-session:consume:{session.Id:N}"
                    : $"consultation-session:release:{session.Id:N}",
                isConsume ? ConsumeReason : ReleaseReason,
                DateTime.UtcNow,
                cancellationToken);

            if (mutationStatus == QuotaMutationStatus.Rejected)
            {
                throw new InvalidOperationException(
                    "The consultation session service credit mutation was rejected.");
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            _logger.LogError(
                ex,
                "Consultation quota finalization failed for session {SessionId} and action {ActionType}.",
                session.Id,
                actionType);
            throw;
        }
    }
}
