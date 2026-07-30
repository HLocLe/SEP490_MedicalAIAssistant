using System.Text.Json;
using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Application.Service;

public sealed class RecoveryPlanCompletionProcessor : IRecoveryPlanCompletionProcessor
{
    private const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecoveryPlanRealtimeNotifier _realtimeNotifier;
    private readonly RecoveryPlanJobOptions _options;
    private readonly ILogger<RecoveryPlanCompletionProcessor> _logger;

    public RecoveryPlanCompletionProcessor(
        IUnitOfWork unitOfWork,
        IRecoveryPlanRealtimeNotifier realtimeNotifier,
        IOptions<RecoveryPlanJobOptions> options,
        ILogger<RecoveryPlanCompletionProcessor> logger)
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
        var candidates = await GetDueCandidatesAsync(utcNow, cancellationToken);

        foreach (var candidate in candidates)
        {
            try
            {
                await ProcessPlanAsync(candidate, utcNow, cancellationToken);
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
                    "Recovery Plan lifecycle event {EventType} for plan {PlanId} targeting {TargetStatus} failed with {FailureType}.",
                    RecoveryPlanLifecycleOutboxEventTypes.Completed,
                    candidate.PlanId,
                    RecoveryPlanStatus.Completed,
                    exception.GetType().Name);
            }
            finally
            {
                _unitOfWork.ClearTrackedChanges();
            }
        }
    }

    private async Task<IReadOnlyList<RecoveryPlanCompletionCandidate>>
        GetDueCandidatesAsync(
            DateTime utcNow,
            CancellationToken cancellationToken)
    {
        var dueCandidates = new List<RecoveryPlanCompletionCandidate>(
            _options.LifecycleBatchSize);
        var maximumEndDate = DateOnly.FromDateTime(utcNow.AddDays(1));
        var pageNumber = 1;

        while (dueCandidates.Count < _options.LifecycleBatchSize)
        {
            var page =
                await _unitOfWork.RecoveryPlans.GetActiveCompletionCandidatesAsync(
                    maximumEndDate,
                    pageNumber,
                    _options.LifecycleBatchSize,
                    cancellationToken);

            foreach (var candidate in page)
            {
                if (IsCompletionDue(candidate.EndDate, candidate.TimeZoneId, utcNow))
                {
                    dueCandidates.Add(candidate);
                    if (dueCandidates.Count == _options.LifecycleBatchSize)
                    {
                        break;
                    }
                }
            }

            if (page.Count < _options.LifecycleBatchSize)
            {
                break;
            }

            pageNumber++;
        }

        return dueCandidates;
    }

    private async Task ProcessPlanAsync(
        RecoveryPlanCompletionCandidate candidate,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var plan = await _unitOfWork.RecoveryPlans.GetByIdForUpdateAsync(
            candidate.PlanId,
            cancellationToken);

        if (!CanComplete(plan, candidate))
        {
            await RollbackAsync();
            return;
        }

        var timeZoneId = await _unitOfWork.RecoveryPlans.GetUserTimeZoneIdAsync(
            plan!.UserId,
            cancellationToken);
        if (!IsCompletionDue(plan.EndDate!.Value, timeZoneId, utcNow, plan.Id))
        {
            await RollbackAsync();
            return;
        }

        CompletePlan(plan, utcNow);
        AddCompletedOutbox(plan, utcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        await _realtimeNotifier.TryNotifyPlanChangedAsync(
            RecoveryPlanRealtimeNotificationFactory.CreatePlanNotification(
                plan,
                RecoveryPlanLifecycleOutboxEventTypes.Completed,
                utcNow),
            CancellationToken.None);
    }

    private static bool CanComplete(
        RecoveryPlan? plan,
        RecoveryPlanCompletionCandidate candidate)
    {
        return plan is
        {
            IsDeleted: false,
            Status: RecoveryPlanStatus.Active,
            EndDate: not null,
            RecoveryPlanRequestId: not null
        }
        && plan.UserId == candidate.UserId;
    }

    private bool IsCompletionDue(
        DateOnly endDate,
        string? timeZoneId,
        DateTime utcNow,
        Guid? planId = null)
    {
        var timeZone = ResolveTimeZone(timeZoneId, planId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
        var localDate = DateOnly.FromDateTime(localNow);
        return localDate > endDate;
    }

    private TimeZoneInfo ResolveTimeZone(string? timeZoneId, Guid? planId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                LogTimeZoneFallback(planId);
            }
            catch (InvalidTimeZoneException)
            {
                LogTimeZoneFallback(planId);
            }
        }
        else
        {
            LogTimeZoneFallback(planId);
        }

        return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
    }

    private void LogTimeZoneFallback(Guid? planId)
    {
        if (!planId.HasValue)
        {
            return;
        }

        _logger.LogWarning(
            "Recovery Plan {PlanId} has no valid user timezone; using the default timezone.",
            planId.Value);
    }

    private static void CompletePlan(RecoveryPlan plan, DateTime utcNow)
    {
        plan.Status = RecoveryPlanStatus.Completed;
        plan.CompletedAt = utcNow;
        plan.IsCurrent = false;
        plan.UpdatedAt = utcNow;
    }

    private void AddCompletedOutbox(RecoveryPlan plan, DateTime utcNow)
    {
        _unitOfWork.RecoveryPlans.AddOutbox(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = RecoveryPlanLifecycleOutboxEventTypes.Completed,
            AggregateType = RecoveryPlanLifecycleOutboxEventTypes.AggregateType,
            AggregateId = plan.Id,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = utcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                PlanId = plan.Id,
                RequestId = plan.RecoveryPlanRequestId!.Value,
                plan.UserId,
                plan.DoctorId,
                Status = plan.Status.ToString(),
                plan.CompletedAt,
                plan.EndDate
            })
        });
    }

    private Task RollbackAsync()
    {
        return _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
    }
}
