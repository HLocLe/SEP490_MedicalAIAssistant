using System.Text.Json;
using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Application.Service;

public sealed class RecoveryPlanAssignmentTimeoutProcessor
    : IRecoveryPlanAssignmentTimeoutProcessor
{
    private const string AssignmentTimeoutReason = "ASSIGNMENT_TIMEOUT";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecoveryPlanRealtimeNotifier _realtimeNotifier;
    private readonly RecoveryPlanJobOptions _options;
    private readonly ILogger<RecoveryPlanAssignmentTimeoutProcessor> _logger;

    public RecoveryPlanAssignmentTimeoutProcessor(
        IUnitOfWork unitOfWork,
        IRecoveryPlanRealtimeNotifier realtimeNotifier,
        IOptions<RecoveryPlanJobOptions> options,
        ILogger<RecoveryPlanAssignmentTimeoutProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessBatchAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var requestIds =
            await _unitOfWork.RecoveryPlanRequests.GetExpiredAssignmentIdsAsync(
                utcNow,
                _options.LifecycleBatchSize,
                cancellationToken);

        foreach (var requestId in requestIds)
        {
            try
            {
                await ProcessRequestAsync(requestId, utcNow, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await RollbackAsync();
                throw;
            }
            catch (Exception exception)
            {
                await RollbackAsync();
                _logger.LogWarning(
                    "Recovery Plan lifecycle event {EventType} for request {RequestId} targeting {TargetStatus} failed with {FailureType}.",
                    RecoveryPlanOutboxEventTypes.Reopened,
                    requestId,
                    RecoveryPlanRequestStatus.WaitingForDoctor,
                    exception.GetType().Name);
            }
            finally
            {
                _unitOfWork.ClearTrackedChanges();
            }
        }
    }

    private async Task ProcessRequestAsync(
        Guid requestId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var request =
            await _unitOfWork.RecoveryPlanRequests.GetByIdForUpdateAsync(
                requestId,
                cancellationToken);

        if (!IsExpiredAssignment(request, utcNow))
        {
            await RollbackAsync();
            return;
        }

        var previousDoctorId = request!.AssignedDoctorId!.Value;
        ReopenRequest(request, utcNow);
        AddReopenedEvent(request, utcNow);
        AddReopenedOutbox(request, previousDoctorId, utcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        var transition = RecoveryPlanRealtimeTransitions.Reopened;
        var notification =
            RecoveryPlanRealtimeNotificationFactory.CreateRequestNotification(
                request,
                transition.EventType,
                transition.ResolveQueueChange(RecoveryPlanRequestStatus.Assigned),
                previousDoctorId,
                utcNow);

        await _realtimeNotifier.TryNotifyRequestChangedAsync(
            notification,
            CancellationToken.None);
    }

    private static bool IsExpiredAssignment(
        RecoveryPlanRequest? request,
        DateTime utcNow)
    {
        return request is
        {
            IsDeleted: false,
            Status: RecoveryPlanRequestStatus.Assigned,
            AssignedDoctorId: not null,
            AssignmentExpiresAt: not null
        }
        && request.AssignmentExpiresAt.Value <= utcNow;
    }

    private static void ReopenRequest(
        RecoveryPlanRequest request,
        DateTime utcNow)
    {
        request.Status = RecoveryPlanRequestStatus.WaitingForDoctor;
        request.AssignedDoctorId = null;
        request.AcceptedAt = null;
        request.ReviewStartedAt = null;
        request.AssignmentExpiresAt = null;
        request.UpdatedAt = utcNow;
        request.Version++;
    }

    private void AddReopenedEvent(
        RecoveryPlanRequest request,
        DateTime utcNow)
    {
        _unitOfWork.RecoveryPlanRequests.AddEvent(new RecoveryPlanRequestEvent
        {
            Id = Guid.NewGuid(),
            RecoveryPlanRequestId = request.Id,
            EventType = RecoveryPlanRequestEventType.Reopened,
            FromStatus = RecoveryPlanRequestStatus.Assigned,
            ToStatus = RecoveryPlanRequestStatus.WaitingForDoctor,
            ActorUserId = null,
            ActorDoctorId = null,
            Reason = AssignmentTimeoutReason,
            CreatedAt = utcNow
        });
    }

    private void AddReopenedOutbox(
        RecoveryPlanRequest request,
        Guid previousDoctorId,
        DateTime utcNow)
    {
        _unitOfWork.RecoveryPlanRequests.AddOutbox(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = RecoveryPlanOutboxEventTypes.Reopened,
            AggregateType = RecoveryPlanOutboxEventTypes.AggregateType,
            AggregateId = request.Id,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = utcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                RequestId = request.Id,
                request.UserId,
                PreviousDoctorId = previousDoctorId,
                Status = request.Status.ToString(),
                ReopenedAtUtc = utcNow
            })
        });
    }

    private Task RollbackAsync()
    {
        return _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
    }
}
