using System.Text.Json;
using MedMateAI.Application.Common;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class RecoveryPlanService : IRecoveryPlanService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecoveryPlanQuotaService _quotaService;
    private readonly IRecoveryPlanClinicalContextService _clinicalContextService;
    private readonly IRecoveryPlanRealtimeNotifier _realtimeNotifier;

    public RecoveryPlanService(
        IUnitOfWork unitOfWork,
        IRecoveryPlanQuotaService quotaService,
        IRecoveryPlanClinicalContextService clinicalContextService,
        IRecoveryPlanRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _quotaService = quotaService;
        _clinicalContextService = clinicalContextService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> CreateDraftAsync(
        Guid doctorUserId,
        Guid requestId,
        CreateRecoveryPlanDraftRequest request,
        CancellationToken cancellationToken)
    {
        var planName = request.PlanName?.Trim() ?? string.Empty;
        var summary = RecoveryPlanValidation.NormalizeOptional(request.Summary);
        var recheckInstruction = RecoveryPlanValidation.NormalizeOptional(
            request.RecheckInstruction);
        var validationError = RecoveryPlanValidation.ValidateDraftHeader(
            planName,
            summary,
            request.DurationDays,
            recheckInstruction);

        if (validationError != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Fail(
                validationError);
        }

        var doctorResult = await GetActiveDoctorAsync(doctorUserId, cancellationToken);
        if (doctorResult.Error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Fail(
                doctorResult.Error);
        }

        var doctor = doctorResult.Doctor!;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var recoveryPlanRequest =
                await _unitOfWork.RecoveryPlanRequests.GetByIdForUpdateAsync(
                    requestId,
                    cancellationToken);

            var accessError = ValidateDraftRequestAccess(recoveryPlanRequest, doctor);
            if (accessError != RecoveryPlanErrorCode.None)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(accessError);
            }

            var existingPlan = await _unitOfWork.RecoveryPlans.GetByRequestIdAsync(
                requestId,
                cancellationToken);

            if (existingPlan is not null)
            {
                if (existingPlan.DoctorId != doctor.Id)
                {
                    return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                        RecoveryPlanErrorCode.Conflict);
                }

                await RollbackAsync();
                var replay = await _unitOfWork.RecoveryPlans.GetDetailByIdAsync(
                    existingPlan.Id,
                    cancellationToken);

                if (replay is null)
                {
                    return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Fail(
                        RecoveryPlanErrorCode.Conflict);
                }

                return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
                    RecoveryPlanMapping.ToDetail(replay),
                    true);
            }

            var utcNow = DateTime.UtcNow;
            var plan = new RecoveryPlan
            {
                Id = Guid.NewGuid(),
                RecoveryPlanRequestId = recoveryPlanRequest!.Id,
                UserId = recoveryPlanRequest.UserId,
                DoctorId = recoveryPlanRequest.AssignedDoctorId,
                TreatmentJourneyId = recoveryPlanRequest.TreatmentJourneyId,
                TestSessionId = recoveryPlanRequest.PrimaryLabTestSessionId,
                SymptomAnalysisSessionId = null,
                PlanName = planName,
                Summary = summary,
                DurationDays = request.DurationDays,
                Status = RecoveryPlanStatus.Draft,
                StartDate = null,
                EndDate = null,
                PublishedAt = null,
                ActivatedAt = null,
                CompletedAt = null,
                ClinicalSnapshotJson = null,
                RecheckInstruction = recheckInstruction,
                IsCurrent = false,
                CreatedAt = utcNow
            };

            _unitOfWork.RecoveryPlans.Add(plan);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
                RecoveryPlanMapping.ToDetail(plan));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public async Task<RecoveryPlanOperationResult<DoctorRecoveryPlanDetailResponse>>
        GetDoctorDetailAsync(
            Guid doctorUserId,
            Guid planId,
            CancellationToken cancellationToken)
    {
        var doctorResult = await GetActiveDoctorAsync(doctorUserId, cancellationToken);
        if (doctorResult.Error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<DoctorRecoveryPlanDetailResponse>.Fail(
                doctorResult.Error);
        }

        var plan = await _unitOfWork.RecoveryPlans.GetDetailByIdAsync(
            planId,
            cancellationToken);
        var doctor = doctorResult.Doctor!;

        if (plan is null
            || !plan.RecoveryPlanRequestId.HasValue
            || plan.RecoveryPlanRequest is null
            || plan.RecoveryPlanRequest.IsDeleted
            || plan.DoctorId != doctor.Id
            || plan.RecoveryPlanRequest.AssignedDoctorId != doctor.Id)
        {
            return RecoveryPlanOperationResult<DoctorRecoveryPlanDetailResponse>.Fail(
                RecoveryPlanErrorCode.NotFound);
        }

        var snapshot = _clinicalContextService.DeserializeSnapshot(
            plan.ClinicalSnapshotJson);
        return RecoveryPlanOperationResult<DoctorRecoveryPlanDetailResponse>.Ok(
            RecoveryPlanMapping.ToDoctorDetail(plan, snapshot));
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> UpdateDraftAsync(
        Guid doctorUserId,
        Guid planId,
        UpdateRecoveryPlanDraftRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync(
            doctorUserId,
            planId,
            (plan, utcNow) => RecoveryPlanDraftMutations.UpdateHeader(plan, request, utcNow),
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<bool>> DeleteDraftAsync(
        Guid doctorUserId,
        Guid planId,
        CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync(
            doctorUserId,
            planId,
            RecoveryPlanDraftMutations.DeletePlan,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>> CreatePhaseAsync(
        Guid doctorUserId,
        Guid planId,
        UpsertRecoveryPlanPhaseRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync<RecoveryPlanPhase, RecoveryPlanPhaseResponse>(
            doctorUserId,
            planId,
            (plan, utcNow) => RecoveryPlanDraftMutations.CreatePhase(plan, request, utcNow),
            RecoveryPlanMapping.ToPhase,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>> UpdatePhaseAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        UpsertRecoveryPlanPhaseRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync(
            doctorUserId,
            planId,
            (plan, utcNow) => RecoveryPlanDraftMutations.UpdatePhase(
                plan,
                phaseId,
                request,
                utcNow),
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<bool>> DeletePhaseAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync(
            doctorUserId,
            planId,
            (plan, utcNow) => RecoveryPlanDraftMutations.DeletePhase(
                plan,
                phaseId,
                utcNow),
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>>
        CreateNutrientAsync(
            Guid doctorUserId,
            Guid planId,
            Guid phaseId,
            UpsertRecoveryPlanNutrientTargetRequest request,
            CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync<
            RecoveryPlanNutrientTarget,
            RecoveryPlanNutrientTargetResponse>(
            doctorUserId,
            planId,
            (plan, utcNow) => RecoveryPlanDraftMutations.CreateNutrient(
                plan,
                phaseId,
                request,
                utcNow),
            RecoveryPlanMapping.ToNutrient,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>>
        UpdateNutrientAsync(
            Guid doctorUserId,
            Guid planId,
            Guid phaseId,
            Guid nutrientId,
            UpsertRecoveryPlanNutrientTargetRequest request,
            CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync(
            doctorUserId,
            planId,
            (plan, utcNow) => RecoveryPlanDraftMutations.UpdateNutrient(
                plan,
                phaseId,
                nutrientId,
                request,
                utcNow),
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<bool>> DeleteNutrientAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync(
            doctorUserId,
            planId,
            (plan, utcNow) => RecoveryPlanDraftMutations.DeleteNutrient(
                plan,
                phaseId,
                nutrientId,
                utcNow),
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>> CreateFoodAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        UpsertRecoveryPlanFoodSourceRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync<RecoveryPlanFoodSource, RecoveryPlanFoodSourceResponse>(
            doctorUserId,
            planId,
            (plan, utcNow) => RecoveryPlanDraftMutations.CreateFood(
                plan,
                phaseId,
                nutrientId,
                request,
                utcNow),
            RecoveryPlanMapping.ToFood,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>> UpdateFoodAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        UpsertRecoveryPlanFoodSourceRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync(
            doctorUserId,
            planId,
            (plan, utcNow) => RecoveryPlanDraftMutations.UpdateFood(
                plan,
                phaseId,
                nutrientId,
                foodSourceId,
                request,
                utcNow),
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<bool>> DeleteFoodAsync(
        Guid doctorUserId,
        Guid planId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        CancellationToken cancellationToken)
    {
        return ExecuteDraftWriteAsync(
            doctorUserId,
            planId,
            (plan, utcNow) => RecoveryPlanDraftMutations.DeleteFood(
                plan,
                phaseId,
                nutrientId,
                foodSourceId,
                utcNow),
            cancellationToken);
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> PublishAsync(
        Guid doctorUserId,
        Guid planId,
        CancellationToken cancellationToken)
    {
        var doctorResult = await GetActiveDoctorAsync(doctorUserId, cancellationToken);
        if (doctorResult.Error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Fail(
                doctorResult.Error);
        }

        var requestId = await _unitOfWork.RecoveryPlans.GetRequestIdByPlanIdAsync(
            planId,
            cancellationToken);
        if (!requestId.HasValue)
        {
            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Fail(
                RecoveryPlanErrorCode.NotFound);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var recoveryPlanRequest =
                await _unitOfWork.RecoveryPlanRequests.GetByIdForUpdateAsync(
                    requestId.Value,
                    cancellationToken);
            var lockedPlan = await _unitOfWork.RecoveryPlans.GetByIdForUpdateAsync(
                planId,
                cancellationToken);

            if (recoveryPlanRequest is null || lockedPlan is null)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            var doctor = doctorResult.Doctor!;
            if (!HasDoctorOwnership(recoveryPlanRequest, lockedPlan, doctor))
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            var plan = await _unitOfWork.RecoveryPlans.GetTrackedDetailAsync(
                planId,
                cancellationToken);
            if (plan is null)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            var replayState = ClassifyPublishState(recoveryPlanRequest, plan);
            if (replayState == PublishState.Replay)
            {
                await RollbackAsync();
                return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
                    RecoveryPlanMapping.ToDetail(plan),
                    true);
            }

            if (replayState == PublishState.Conflict)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.Conflict);
            }

            if (plan.Status != RecoveryPlanStatus.Draft)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.RecoveryPlanNotEditable);
            }

            if (recoveryPlanRequest.Status != RecoveryPlanRequestStatus.InReview)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.InvalidRequestState);
            }

            var structureError = RecoveryPlanValidation.ValidateCompletePlan(plan);
            if (structureError != RecoveryPlanErrorCode.None)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    structureError);
            }

            var utcNow = DateTime.UtcNow;
            var snapshot = await _clinicalContextService.BuildSnapshotAsync(
                recoveryPlanRequest.Id,
                utcNow,
                cancellationToken);
            if (snapshot is null)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.Conflict);
            }

            var usage = await _unitOfWork.QuotaUsages.GetByIdForQuotaAsync(
                recoveryPlanRequest.UserSubscriptionUsageId,
                recoveryPlanRequest.UserSubscriptionId,
                IServiceCreditService.QuotaCode,
                cancellationToken);
            if (usage is null)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.QuotaMutationFailed);
            }

            var consumeStatus = await _quotaService.ConsumeAsync(
                usage.Id,
                recoveryPlanRequest.UserSubscriptionId,
                usage.QuotaId,
                recoveryPlanRequest.Id,
                doctorUserId,
                BuildPublishConsumeKey(recoveryPlanRequest.Id),
                utcNow,
                cancellationToken);

            switch (consumeStatus)
            {
                case QuotaMutationStatus.Applied:
                    break;
                case QuotaMutationStatus.Duplicate:
                    await RollbackAsync();
                    return await LoadPublishReplayAsync(
                        doctor.Id,
                        planId,
                        requestId.Value,
                        cancellationToken);
                case QuotaMutationStatus.Rejected:
                    return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                        RecoveryPlanErrorCode.QuotaMutationFailed);
                default:
                    throw new ArgumentOutOfRangeException(nameof(consumeStatus));
            }

            plan.Status = RecoveryPlanStatus.ReadyToStart;
            plan.PublishedAt = utcNow;
            plan.ClinicalSnapshotJson = _clinicalContextService.SerializeSnapshot(snapshot);
            plan.StartDate = null;
            plan.EndDate = null;
            plan.ActivatedAt = null;
            plan.IsCurrent = false;
            plan.UpdatedAt = utcNow;

            recoveryPlanRequest.Status = RecoveryPlanRequestStatus.Published;
            recoveryPlanRequest.PublishedAt = utcNow;
            recoveryPlanRequest.AssignmentExpiresAt = null;
            recoveryPlanRequest.Version++;
            recoveryPlanRequest.UpdatedAt = utcNow;

            AddPublishedEvent(recoveryPlanRequest, doctor, doctorUserId, utcNow);
            AddReadyOutbox(plan, recoveryPlanRequest, doctor, utcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            await _realtimeNotifier.TryNotifyPlanChangedAsync(
                RecoveryPlanRealtimeNotificationFactory.CreatePlanNotification(
                    plan,
                    RecoveryPlanLifecycleOutboxEventTypes.Ready,
                    utcNow),
                CancellationToken.None);

            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
                RecoveryPlanMapping.ToDetail(plan));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public async Task<RecoveryPlanOperationResult<PagedResponse<RecoveryPlanSummaryResponse>>>
        GetMineAsync(
            Guid userId,
            PaginationQuery page,
            RecoveryPlanStatus? status,
            CancellationToken cancellationToken)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            return RecoveryPlanOperationResult<PagedResponse<RecoveryPlanSummaryResponse>>.Fail(
                RecoveryPlanErrorCode.InvalidRequest);
        }

        if (status == RecoveryPlanStatus.Draft)
        {
            return RecoveryPlanOperationResult<PagedResponse<RecoveryPlanSummaryResponse>>.Fail(
                RecoveryPlanErrorCode.Forbidden);
        }

        var plans = await _unitOfWork.RecoveryPlans.GetUserPlansPagedAsync(
            userId,
            page.PageNumber,
            page.PageSize,
            status,
            cancellationToken);

        return RecoveryPlanOperationResult<PagedResponse<RecoveryPlanSummaryResponse>>.Ok(
            RecoveryPlanMapping.ToPage(plans));
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>>
        GetUserDetailAsync(
            Guid userId,
            Guid planId,
            CancellationToken cancellationToken)
    {
        var plan = await _unitOfWork.RecoveryPlans.GetDetailByIdAsync(
            planId,
            cancellationToken);

        if (plan is null
            || plan.UserId != userId
            || !plan.RecoveryPlanRequestId.HasValue
            || plan.Status == RecoveryPlanStatus.Draft)
        {
            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Fail(
                RecoveryPlanErrorCode.NotFound);
        }

        return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
            RecoveryPlanMapping.ToDetail(plan));
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> StartAsync(
        Guid userId,
        Guid planId,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var userLocked = await _unitOfWork.RecoveryPlanRequests
                .LockUserRecoveryPlanWorkflowAsync(userId, cancellationToken);
            if (!userLocked)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            var lockedPlan = await _unitOfWork.RecoveryPlans.GetByIdForUpdateAsync(
                planId,
                cancellationToken);
            if (lockedPlan is null || lockedPlan.UserId != userId)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            var plan = await _unitOfWork.RecoveryPlans.GetTrackedDetailAsync(
                planId,
                cancellationToken);
            if (plan is null)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            if (!plan.RecoveryPlanRequestId.HasValue)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.Conflict);
            }

            if (plan.Status == RecoveryPlanStatus.Active)
            {
                await RollbackAsync();
                return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
                    RecoveryPlanMapping.ToDetail(plan),
                    true);
            }

            if (plan.Status == RecoveryPlanStatus.Draft)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.Forbidden);
            }

            if (plan.Status != RecoveryPlanStatus.ReadyToStart)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.InvalidRequestState);
            }

            if (plan.DurationDays <= 0)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.InvalidPlanStructure);
            }

            var hasBlockingPlan = await _unitOfWork.RecoveryPlans
                .HasBlockingPlanForUserAsync(userId, plan.Id, cancellationToken);
            if (hasBlockingPlan)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.RecoveryPlanWorkflowAlreadyActive,
                    "You already have a recovery plan request or plan in progress.");
            }

            var timeZoneId = await _unitOfWork.RecoveryPlans.GetUserTimeZoneIdAsync(
                userId,
                cancellationToken);
            var timeZone = ResolveTimeZone(timeZoneId);
            if (timeZone is null)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.InvalidUserTimeZone);
            }

            var utcNow = DateTime.UtcNow;
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            var startDate = DateOnly.FromDateTime(localNow);
            var endDate = startDate.AddDays(plan.DurationDays - 1);

            plan.Status = RecoveryPlanStatus.Active;
            plan.ActivatedAt = utcNow;
            plan.StartDate = startDate;
            plan.EndDate = endDate;
            plan.IsCurrent = true;
            plan.UpdatedAt = utcNow;

            AddActivatedOutbox(plan, utcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            await _realtimeNotifier.TryNotifyPlanChangedAsync(
                RecoveryPlanRealtimeNotificationFactory.CreatePlanNotification(
                    plan,
                    RecoveryPlanLifecycleOutboxEventTypes.Activated,
                    utcNow),
                CancellationToken.None);

            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
                RecoveryPlanMapping.ToDetail(plan));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>> CancelAsync(
        Guid userId,
        Guid planId,
        CancelRecoveryPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!RecoveryPlanCancellationReasons.TryNormalize(
                request.CancellationReasonCode,
                request.CancellationReason,
                out var cancellationReasonCode,
                out var cancellationReason))
        {
            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Fail(
                RecoveryPlanErrorCode.InvalidRequest);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var userLocked = await _unitOfWork.RecoveryPlanRequests
                .LockUserRecoveryPlanWorkflowAsync(userId, cancellationToken);
            if (!userLocked)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            var lockedPlan = await _unitOfWork.RecoveryPlans.GetByIdForUpdateAsync(
                planId,
                cancellationToken);
            if (lockedPlan is null
                || lockedPlan.UserId != userId
                || lockedPlan.Status == RecoveryPlanStatus.Draft)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            var plan = await _unitOfWork.RecoveryPlans.GetTrackedDetailAsync(
                planId,
                cancellationToken);
            if (plan is null)
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            if (plan.Status == RecoveryPlanStatus.Cancelled)
            {
                await RollbackAsync();
                return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
                    RecoveryPlanMapping.ToDetail(plan),
                    true,
                    "Recovery plan was already cancelled.");
            }

            if (plan.Status is not (RecoveryPlanStatus.ReadyToStart
                or RecoveryPlanStatus.Active))
            {
                return await RollbackFailureAsync<RecoveryPlanDetailResponse>(
                    RecoveryPlanErrorCode.RecoveryPlanNotCancellable,
                    "Only ReadyToStart or Active recovery plans can be cancelled.");
            }

            var utcNow = DateTime.UtcNow;
            plan.Status = RecoveryPlanStatus.Cancelled;
            plan.IsCurrent = false;
            plan.CancelledAt = utcNow;
            plan.CancelledByUserId = userId;
            plan.CancellationReasonCode = cancellationReasonCode;
            plan.CancellationReason = cancellationReason;
            plan.UpdatedAt = utcNow;

            AddCancelledOutbox(plan, utcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var response = RecoveryPlanMapping.ToDetail(plan);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            await _realtimeNotifier.TryNotifyPlanChangedAsync(
                RecoveryPlanRealtimeNotificationFactory.CreatePlanNotification(
                    plan,
                    RecoveryPlanLifecycleOutboxEventTypes.Cancelled,
                    utcNow),
                CancellationToken.None);

            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
                response,
                message: "Recovery plan cancelled.");
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    private async Task<RecoveryPlanOperationResult<TResponse>> ExecuteDraftWriteAsync<TResponse>(
        Guid doctorUserId,
        Guid planId,
        Func<RecoveryPlan, DateTime, RecoveryPlanOperationResult<TResponse>> mutation,
        CancellationToken cancellationToken)
    {
        return await ExecuteDraftWriteAsync(
            doctorUserId,
            planId,
            mutation,
            static response => response,
            cancellationToken);
    }

    private async Task<RecoveryPlanOperationResult<TResponse>> ExecuteDraftWriteAsync<
        TMutation,
        TResponse>(
        Guid doctorUserId,
        Guid planId,
        Func<RecoveryPlan, DateTime, RecoveryPlanOperationResult<TMutation>> mutation,
        Func<TMutation, TResponse> mapResponse,
        CancellationToken cancellationToken)
    {
        var doctorResult = await GetActiveDoctorAsync(doctorUserId, cancellationToken);
        if (doctorResult.Error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<TResponse>.Fail(doctorResult.Error);
        }

        var requestId = await _unitOfWork.RecoveryPlans.GetRequestIdByPlanIdAsync(
            planId,
            cancellationToken);
        if (!requestId.HasValue)
        {
            return RecoveryPlanOperationResult<TResponse>.Fail(RecoveryPlanErrorCode.NotFound);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var recoveryPlanRequest =
                await _unitOfWork.RecoveryPlanRequests.GetByIdForUpdateAsync(
                    requestId.Value,
                    cancellationToken);
            var lockedPlan = await _unitOfWork.RecoveryPlans.GetByIdForUpdateAsync(
                planId,
                cancellationToken);

            if (recoveryPlanRequest is null || lockedPlan is null)
            {
                return await RollbackFailureAsync<TResponse>(RecoveryPlanErrorCode.NotFound);
            }

            var doctor = doctorResult.Doctor!;
            if (!HasDoctorOwnership(recoveryPlanRequest, lockedPlan, doctor))
            {
                return await RollbackFailureAsync<TResponse>(RecoveryPlanErrorCode.NotFound);
            }

            if (lockedPlan.Status != RecoveryPlanStatus.Draft)
            {
                return await RollbackFailureAsync<TResponse>(
                    RecoveryPlanErrorCode.RecoveryPlanNotEditable);
            }

            if (!CanEditDraft(recoveryPlanRequest.Status))
            {
                return await RollbackFailureAsync<TResponse>(
                    RecoveryPlanErrorCode.InvalidRequestState);
            }

            var plan = await _unitOfWork.RecoveryPlans.GetTrackedDetailAsync(
                planId,
                cancellationToken);
            if (plan is null)
            {
                return await RollbackFailureAsync<TResponse>(RecoveryPlanErrorCode.NotFound);
            }

            var mutationResult = mutation(plan, DateTime.UtcNow);
            if (!mutationResult.Success)
            {
                return await RollbackFailureAsync<TResponse>(
                    mutationResult.Error,
                    mutationResult.Message);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var response = mapResponse(mutationResult.Data!);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return RecoveryPlanOperationResult<TResponse>.Ok(
                response,
                mutationResult.IsReplay);
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    private async Task<(Doctor? Doctor, RecoveryPlanErrorCode Error)> GetActiveDoctorAsync(
        Guid doctorUserId,
        CancellationToken cancellationToken)
    {
        var doctor = await _unitOfWork.RecoveryPlanRequests.GetDoctorByUserIdAsync(
            doctorUserId,
            cancellationToken);

        if (doctor is null)
        {
            return (null, RecoveryPlanErrorCode.DoctorProfileNotFound);
        }

        if (!doctor.IsActive)
        {
            return (null, RecoveryPlanErrorCode.DoctorNotActive);
        }

        return (doctor, RecoveryPlanErrorCode.None);
    }

    private async Task<RecoveryPlanOperationResult<RecoveryPlanDetailResponse>>
        LoadPublishReplayAsync(
            Guid doctorId,
            Guid planId,
            Guid requestId,
            CancellationToken cancellationToken)
    {
        var plan = await _unitOfWork.RecoveryPlans.GetDetailByIdAsync(
            planId,
            cancellationToken);

        if (plan?.RecoveryPlanRequest is null
            || plan.RecoveryPlanRequestId != requestId
            || plan.DoctorId != doctorId
            || plan.RecoveryPlanRequest.AssignedDoctorId != doctorId
            || plan.RecoveryPlanRequest.Status != RecoveryPlanRequestStatus.Published
            || !IsPublishedPlanStatus(plan.Status))
        {
            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Fail(
                RecoveryPlanErrorCode.Conflict);
        }

        return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
            RecoveryPlanMapping.ToDetail(plan),
            true);
    }

    private static RecoveryPlanErrorCode ValidateDraftRequestAccess(
        RecoveryPlanRequest? request,
        Doctor doctor)
    {
        if (request is null || request.AssignedDoctorId != doctor.Id)
        {
            return RecoveryPlanErrorCode.NotFound;
        }

        if (!CanEditDraft(request.Status))
        {
            return RecoveryPlanErrorCode.InvalidRequestState;
        }

        return RecoveryPlanErrorCode.None;
    }

    private static bool HasDoctorOwnership(
        RecoveryPlanRequest request,
        RecoveryPlan plan,
        Doctor doctor)
    {
        return plan.RecoveryPlanRequestId == request.Id
            && request.AssignedDoctorId == doctor.Id
            && plan.DoctorId == doctor.Id;
    }

    private static PublishState ClassifyPublishState(
        RecoveryPlanRequest request,
        RecoveryPlan plan)
    {
        var requestPublished = request.Status == RecoveryPlanRequestStatus.Published;
        var planPublished = IsPublishedPlanStatus(plan.Status);

        if (requestPublished && planPublished)
        {
            return PublishState.Replay;
        }

        if (requestPublished || planPublished)
        {
            return PublishState.Conflict;
        }

        return PublishState.New;
    }

    private static bool IsPublishedPlanStatus(RecoveryPlanStatus status)
    {
        return status is RecoveryPlanStatus.ReadyToStart
            or RecoveryPlanStatus.Active
            or RecoveryPlanStatus.Completed;
    }

    private static bool CanEditDraft(RecoveryPlanRequestStatus status)
    {
        return status is RecoveryPlanRequestStatus.InReview
            or RecoveryPlanRequestStatus.NeedMoreInformation;
    }

    private static string BuildPublishConsumeKey(Guid requestId)
    {
        return $"credit:consume:recovery-plan:{requestId:N}";
    }

    private void AddPublishedEvent(
        RecoveryPlanRequest request,
        Doctor doctor,
        Guid doctorUserId,
        DateTime utcNow)
    {
        _unitOfWork.RecoveryPlanRequests.AddEvent(new RecoveryPlanRequestEvent
        {
            Id = Guid.NewGuid(),
            RecoveryPlanRequestId = request.Id,
            EventType = RecoveryPlanRequestEventType.Published,
            FromStatus = RecoveryPlanRequestStatus.InReview,
            ToStatus = RecoveryPlanRequestStatus.Published,
            ActorUserId = doctorUserId,
            ActorDoctorId = doctor.Id,
            Reason = null,
            CreatedAt = utcNow
        });
    }

    private void AddReadyOutbox(
        RecoveryPlan plan,
        RecoveryPlanRequest request,
        Doctor doctor,
        DateTime utcNow)
    {
        _unitOfWork.RecoveryPlans.AddOutbox(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = RecoveryPlanLifecycleOutboxEventTypes.Ready,
            AggregateType = RecoveryPlanLifecycleOutboxEventTypes.AggregateType,
            AggregateId = plan.Id,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = utcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                PlanId = plan.Id,
                RequestId = request.Id,
                plan.UserId,
                DoctorId = doctor.Id,
                Status = plan.Status.ToString(),
                plan.PublishedAt
            })
        });
    }

    private void AddActivatedOutbox(RecoveryPlan plan, DateTime utcNow)
    {
        if (!plan.RecoveryPlanRequestId.HasValue)
        {
            throw new InvalidOperationException(
                "A request-based recovery plan is required before activation.");
        }

        var requestId = plan.RecoveryPlanRequestId.Value;

        _unitOfWork.RecoveryPlans.AddOutbox(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = RecoveryPlanLifecycleOutboxEventTypes.Activated,
            AggregateType = RecoveryPlanLifecycleOutboxEventTypes.AggregateType,
            AggregateId = plan.Id,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = utcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                PlanId = plan.Id,
                RequestId = requestId,
                plan.UserId,
                Status = plan.Status.ToString(),
                plan.ActivatedAt,
                plan.StartDate,
                plan.EndDate
            })
        });
    }

    private void AddCancelledOutbox(RecoveryPlan plan, DateTime utcNow)
    {
        if (!plan.RecoveryPlanRequestId.HasValue)
        {
            throw new InvalidOperationException(
                "A request-based recovery plan is required before cancellation.");
        }

        _unitOfWork.RecoveryPlans.AddOutbox(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = RecoveryPlanLifecycleOutboxEventTypes.Cancelled,
            AggregateType = RecoveryPlanLifecycleOutboxEventTypes.AggregateType,
            AggregateId = plan.Id,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = utcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                PlanId = plan.Id,
                RequestId = plan.RecoveryPlanRequestId.Value,
                plan.UserId,
                Status = plan.Status.ToString(),
                plan.CancelledAt
            })
        });
    }

    private static TimeZoneInfo? ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    private async Task<RecoveryPlanOperationResult<TResponse>> RollbackFailureAsync<TResponse>(
        RecoveryPlanErrorCode error,
        string? message = null)
    {
        await RollbackAsync();
        return RecoveryPlanOperationResult<TResponse>.Fail(error, message);
    }

    private Task RollbackAsync()
    {
        return _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
    }

    private enum PublishState
    {
        New,
        Replay,
        Conflict
    }
}
