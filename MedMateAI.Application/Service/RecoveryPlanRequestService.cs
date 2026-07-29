using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedMateAI.Application.Common;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlanRequests;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Options;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.Extensions.Options;

namespace MedMateAI.Application.Service;

public sealed class RecoveryPlanRequestService : IRecoveryPlanRequestService
{
    private const int MaximumIdempotencyKeyLength = 100;
    private const int MaximumReasonCodeLength = 100;
    private const int MaximumRequestTextLength = 2000;
    private const string AdditionalInformationProvidedEventReason =
        "Additional information provided by request owner.";
    private const string RejectedByDoctorEventReason =
        "Recovery plan request rejected by assigned doctor.";

    private readonly IUnitOfWork _uow;
    private readonly IRecoveryPlanQuotaService _quota;
    private readonly RecoveryPlanOptions _options;

    public RecoveryPlanRequestService(IUnitOfWork uow, IRecoveryPlanQuotaService quota, IOptions<RecoveryPlanOptions> options)
    {
        _uow = uow;
        _quota = quota;
        _options = options.Value;
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> CreateAsync(
        Guid userId,
        string idempotencyKey,
        CreateRecoveryPlanRequest request,
        CancellationToken cancellationToken)
    {
        idempotencyKey = idempotencyKey?.Trim() ?? string.Empty;
        if (idempotencyKey.Length is < 1 or > MaximumIdempotencyKeyLength)
        {
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.IdempotencyKeyInvalid);
        }

        var requestNote = request.RequestNote?.Trim();
        if (!Enum.IsDefined(request.DiseaseGroup) || requestNote?.Length > MaximumRequestTextLength)
        {
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.InvalidRequest);
        }

        if (request.TreatmentJourneyId.HasValue &&
            !await _uow.RecoveryPlanRequests.IsOwnedTreatmentJourneyAsync(
                request.TreatmentJourneyId.Value,
                userId,
                cancellationToken))
        {
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.NotFound);
        }

        if (request.PrimaryLabTestSessionId.HasValue &&
            !await _uow.RecoveryPlanRequests.IsOwnedLabSessionAsync(
                request.PrimaryLabTestSessionId.Value,
                userId,
                cancellationToken))
        {
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.NotFound);
        }

        var scopedIdempotencyKey = BuildCreateIdempotencyKey(userId, idempotencyKey);
        var replay = await LoadIdempotentReplayAsync(userId, scopedIdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(replay), true);
        }

        var requestId = Guid.NewGuid();
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var utcNow = DateTime.UtcNow;
            var usageResult = await _quota.ResolveUsageAsync(userId, utcNow, cancellationToken);
            if (!usageResult.Success || usageResult.Data is null)
            {
                await RollbackAsync();
                return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(usageResult.Error);
            }

            var usage = usageResult.Data;
            var quotaReserved = await _quota.ReserveAsync(
                usage.Id,
                usage.UserSubscriptionId,
                usage.QuotaId,
                requestId,
                userId,
                scopedIdempotencyKey,
                utcNow,
                cancellationToken);

            if (!quotaReserved)
            {
                await RollbackAsync();
                replay = await LoadIdempotentReplayAsync(userId, scopedIdempotencyKey, cancellationToken);
                if (replay is not null)
                {
                    return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(replay), true);
                }

                return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                    RecoveryPlanErrorCode.RecoveryPlanQuotaExhausted);
            }

            var recoveryPlanRequest = new RecoveryPlanRequest
            {
                Id = requestId,
                UserId = userId,
                DiseaseGroup = request.DiseaseGroup,
                TreatmentJourneyId = request.TreatmentJourneyId,
                PrimaryLabTestSessionId = request.PrimaryLabTestSessionId,
                UserSubscriptionId = usage.UserSubscriptionId,
                UserSubscriptionUsageId = usage.Id,
                Status = RecoveryPlanRequestStatus.WaitingForDoctor,
                RequestNote = EmptyToNull(requestNote),
                RequestedAt = utcNow,
                CreatedAt = utcNow,
                Version = 0
            };

            _uow.RecoveryPlanRequests.Add(recoveryPlanRequest);
            AddRequestEvent(
                recoveryPlanRequest,
                RecoveryPlanRequestEventType.Created,
                null,
                recoveryPlanRequest.Status,
                userId,
                null,
                null,
                utcNow);
            AddRequestEvent(
                recoveryPlanRequest,
                RecoveryPlanRequestEventType.QuotaReserved,
                recoveryPlanRequest.Status,
                recoveryPlanRequest.Status,
                userId,
                null,
                null,
                utcNow);
            AddOutboxMessage(recoveryPlanRequest, RecoveryPlanOutboxEventTypes.Created, utcNow);

            await _uow.SaveChangesAsync(cancellationToken);
            await _uow.CommitTransactionAsync(cancellationToken);

            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(recoveryPlanRequest));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public async Task<RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>> GetMineAsync(
        Guid userId,
        PaginationQuery page,
        RecoveryPlanRequestStatus? status,
        CancellationToken cancellationToken)
    {
        var requests = await _uow.RecoveryPlanRequests.GetByUserPagedAsync(
            userId,
            page.PageNumber,
            page.PageSize,
            status,
            cancellationToken);

        return RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>.Ok(ToPage(requests, Map));
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> GetDetailAsync(
        Guid userId,
        bool isDoctor,
        bool isAdmin,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await _uow.RecoveryPlanRequests.GetByIdAsync(requestId, cancellationToken);
        if (request is null)
        {
            return FailNotFound();
        }

        if (request.UserId == userId || isAdmin)
        {
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request));
        }

        if (!isDoctor)
        {
            return FailNotFound();
        }

        var doctor = await _uow.RecoveryPlanRequests.GetDoctorByUserIdAsync(userId, cancellationToken);
        if (doctor is null || request.AssignedDoctorId != doctor.Id)
        {
            return FailNotFound();
        }

        return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request));
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> CancelAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        UserTransitionAsync(
            userId,
            requestId,
            cancellationToken,
            CancelRequestAsync);

    public Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> ProvideInformationAsync(
        Guid userId,
        Guid requestId,
        string information,
        CancellationToken cancellationToken)
    {
        var normalizedInformation = information?.Trim() ?? string.Empty;
        if (normalizedInformation.Length is < 1 or > MaximumRequestTextLength)
        {
            return InvalidRequestTask();
        }

        return UserTransitionAsync(
            userId,
            requestId,
            cancellationToken,
            (request, actorUserId, utcNow) =>
                ProvideMoreInformation(request, actorUserId, normalizedInformation, utcNow));
    }

    public async Task<RecoveryPlanOperationResult<PagedResponse<OpenRecoveryPlanRequestResponse>>> GetOpenAsync(
        Guid doctorUserId,
        PaginationQuery page,
        RecoveryPlanDiseaseGroup? diseaseGroup,
        CancellationToken cancellationToken)
    {
        var doctor = await _uow.RecoveryPlanRequests.GetDoctorByUserIdAsync(doctorUserId, cancellationToken);
        var error = ValidateDoctor(doctor);
        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<PagedResponse<OpenRecoveryPlanRequestResponse>>.Fail(error);
        }

        var requests = await _uow.RecoveryPlanRequests.GetOpenPagedAsync(
            page.PageNumber,
            page.PageSize,
            diseaseGroup,
            cancellationToken);

        return RecoveryPlanOperationResult<PagedResponse<OpenRecoveryPlanRequestResponse>>.Ok(
            ToPage(requests, MapOpen));
    }

    public async Task<RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>> GetDoctorMineAsync(
        Guid doctorUserId,
        PaginationQuery page,
        RecoveryPlanRequestStatus? status,
        CancellationToken cancellationToken)
    {
        var doctor = await _uow.RecoveryPlanRequests.GetDoctorByUserIdAsync(doctorUserId, cancellationToken);
        if (doctor is null)
        {
            return RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>.Fail(
                RecoveryPlanErrorCode.DoctorProfileNotFound);
        }

        var requests = await _uow.RecoveryPlanRequests.GetAssignedToDoctorPagedAsync(
            doctor.Id,
            page.PageNumber,
            page.PageSize,
            status,
            cancellationToken);

        return RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>.Ok(ToPage(requests, Map));
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> AcceptAsync(
        Guid doctorUserId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var doctor = await _uow.RecoveryPlanRequests.GetDoctorByUserIdForUpdateAsync(
                doctorUserId,
                cancellationToken);
            var error = ValidateDoctor(doctor);
            if (error != RecoveryPlanErrorCode.None)
            {
                return await RollbackFailureAsync(error);
            }

            var currentRequest = await _uow.RecoveryPlanRequests.GetByIdAsync(requestId, cancellationToken);
            var currentResult = ClassifyAcceptState(currentRequest, doctor!.Id);
            if (currentResult is not null)
            {
                await RollbackAsync();
                return currentResult;
            }

            var activeAssignmentCount = await _uow.RecoveryPlanRequests.CountActiveAssignmentsAsync(
                doctor.Id,
                cancellationToken);
            if (doctor.MaxConcurrentRecoveryPlanRequests.HasValue &&
                activeAssignmentCount >= doctor.MaxConcurrentRecoveryPlanRequests.Value)
            {
                return await RollbackFailureAsync(RecoveryPlanErrorCode.DoctorCapacityReached);
            }

            var utcNow = DateTime.UtcNow;
            var acceptedRequest = await _uow.RecoveryPlanRequests.TryAcceptAsync(
                requestId,
                doctor.Id,
                utcNow,
                utcNow.AddMinutes(_options.AssignmentTimeoutMinutes),
                utcNow,
                cancellationToken);
            if (acceptedRequest is null)
            {
                currentRequest = await _uow.RecoveryPlanRequests.GetByIdAsync(requestId, cancellationToken);
                await RollbackAsync();

                currentResult = ClassifyAcceptState(currentRequest, doctor.Id);
                if (currentResult is not null)
                {
                    return currentResult;
                }

                return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                    RecoveryPlanErrorCode.InvalidRequestState);
            }

            AddRequestEvent(
                acceptedRequest,
                RecoveryPlanRequestEventType.Accepted,
                RecoveryPlanRequestStatus.WaitingForDoctor,
                RecoveryPlanRequestStatus.Assigned,
                null,
                doctor.Id,
                null,
                utcNow);
            AddOutboxMessage(acceptedRequest, RecoveryPlanOutboxEventTypes.Claimed, utcNow);

            await _uow.SaveChangesAsync(cancellationToken);
            await _uow.CommitTransactionAsync(cancellationToken);

            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(acceptedRequest));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> StartReviewAsync(
        Guid doctorUserId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        DoctorTransitionAsync(
            doctorUserId,
            requestId,
            cancellationToken,
            StartReview);

    public Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> ReleaseAsync(
        Guid doctorUserId,
        Guid requestId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var normalizedReason = EmptyToNull(reason?.Trim());
        if (normalizedReason?.Length > MaximumRequestTextLength)
        {
            return InvalidRequestTask();
        }

        return DoctorTransitionAsync(
            doctorUserId,
            requestId,
            cancellationToken,
            (request, doctor, utcNow) => ReleaseRequest(request, doctor, normalizedReason, utcNow));
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> RequestInformationAsync(
        Guid doctorUserId,
        Guid requestId,
        string reason,
        CancellationToken cancellationToken)
    {
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length is < 1 or > MaximumRequestTextLength)
        {
            return InvalidRequestTask();
        }

        return DoctorTransitionAsync(
            doctorUserId,
            requestId,
            cancellationToken,
            (request, doctor, utcNow) =>
                RequestMoreInformation(request, doctor, normalizedReason, utcNow));
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> RejectAsync(
        Guid doctorUserId,
        Guid requestId,
        string code,
        string reason,
        CancellationToken cancellationToken)
    {
        code = code?.Trim() ?? string.Empty;
        reason = reason?.Trim() ?? string.Empty;
        if (code.Length is < 1 or > MaximumReasonCodeLength ||
            reason.Length is < 1 or > MaximumRequestTextLength)
        {
            return await InvalidRequestTask();
        }

        return await DoctorTransitionAsync(
            doctorUserId,
            requestId,
            cancellationToken,
            (request, doctor, utcNow, transitionCancellationToken) =>
                RejectRequestAsync(
                    request,
                    doctor,
                    doctorUserId,
                    code,
                    reason,
                    utcNow,
                    transitionCancellationToken));
    }

    private async Task<RecoveryPlanErrorCode> CancelRequestAsync(
        RecoveryPlanRequest request,
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (request.Status == RecoveryPlanRequestStatus.Cancelled)
        {
            return RecoveryPlanErrorCode.None;
        }

        if (!CanCancel(request.Status))
        {
            return RecoveryPlanErrorCode.InvalidRequestState;
        }

        var quotaId = await GetQuotaIdAsync(request.UserSubscriptionUsageId, cancellationToken);
        var quotaReleased = await _quota.ReleaseAsync(
            request.UserSubscriptionUsageId,
            request.UserSubscriptionId,
            quotaId,
            request.Id,
            userId,
            BuildCancelQuotaReleaseKey(request.Id),
            utcNow,
            cancellationToken);
        if (!quotaReleased)
        {
            return RecoveryPlanErrorCode.QuotaMutationFailed;
        }

        var previousStatus = request.Status;
        request.Status = RecoveryPlanRequestStatus.Cancelled;
        request.CancelledAt = utcNow;
        request.AssignmentExpiresAt = null;
        MarkUpdated(request, utcNow);

        AddRequestEvent(
            request,
            RecoveryPlanRequestEventType.Cancelled,
            previousStatus,
            request.Status,
            userId,
            null,
            null,
            utcNow);
        AddOutboxMessage(request, RecoveryPlanOutboxEventTypes.Cancelled, utcNow);

        return RecoveryPlanErrorCode.None;
    }

    private RecoveryPlanErrorCode ProvideMoreInformation(
        RecoveryPlanRequest request,
        Guid userId,
        string information,
        DateTime utcNow)
    {
        if (request.Status != RecoveryPlanRequestStatus.NeedMoreInformation)
        {
            return RecoveryPlanErrorCode.InvalidRequestState;
        }

        request.RequestNote = information;
        request.Status = RecoveryPlanRequestStatus.InReview;
        MarkUpdated(request, utcNow);

        AddRequestEvent(
            request,
            RecoveryPlanRequestEventType.Reopened,
            RecoveryPlanRequestStatus.NeedMoreInformation,
            request.Status,
            userId,
            null,
            AdditionalInformationProvidedEventReason,
            utcNow);
        AddOutboxMessage(request, RecoveryPlanOutboxEventTypes.InformationProvided, utcNow);

        return RecoveryPlanErrorCode.None;
    }

    private RecoveryPlanErrorCode StartReview(
        RecoveryPlanRequest request,
        Doctor doctor,
        DateTime utcNow)
    {
        if (request.Status == RecoveryPlanRequestStatus.InReview)
        {
            return RecoveryPlanErrorCode.None;
        }

        if (request.Status != RecoveryPlanRequestStatus.Assigned)
        {
            return RecoveryPlanErrorCode.InvalidRequestState;
        }

        if (request.AssignmentExpiresAt <= utcNow)
        {
            return RecoveryPlanErrorCode.AssignmentExpired;
        }

        request.Status = RecoveryPlanRequestStatus.InReview;
        request.ReviewStartedAt = utcNow;
        request.AssignmentExpiresAt = null;
        MarkUpdated(request, utcNow);

        AddRequestEvent(
            request,
            RecoveryPlanRequestEventType.ReviewStarted,
            RecoveryPlanRequestStatus.Assigned,
            request.Status,
            null,
            doctor.Id,
            null,
            utcNow);
        AddOutboxMessage(request, RecoveryPlanOutboxEventTypes.ReviewStarted, utcNow);

        return RecoveryPlanErrorCode.None;
    }

    private RecoveryPlanErrorCode ReleaseRequest(
        RecoveryPlanRequest request,
        Doctor doctor,
        string? reason,
        DateTime utcNow)
    {
        if (!CanRelease(request.Status))
        {
            return RecoveryPlanErrorCode.InvalidRequestState;
        }

        var previousStatus = request.Status;
        request.Status = RecoveryPlanRequestStatus.WaitingForDoctor;
        request.AssignedDoctorId = null;
        request.AcceptedAt = null;
        request.ReviewStartedAt = null;
        request.AssignmentExpiresAt = null;
        MarkUpdated(request, utcNow);

        AddRequestEvent(
            request,
            RecoveryPlanRequestEventType.Released,
            previousStatus,
            request.Status,
            null,
            doctor.Id,
            reason,
            utcNow);
        AddOutboxMessage(request, RecoveryPlanOutboxEventTypes.Released, utcNow);

        return RecoveryPlanErrorCode.None;
    }

    private RecoveryPlanErrorCode RequestMoreInformation(
        RecoveryPlanRequest request,
        Doctor doctor,
        string reason,
        DateTime utcNow)
    {
        if (request.Status == RecoveryPlanRequestStatus.NeedMoreInformation)
        {
            return RecoveryPlanErrorCode.None;
        }

        if (request.Status != RecoveryPlanRequestStatus.InReview)
        {
            return RecoveryPlanErrorCode.InvalidRequestState;
        }

        request.Status = RecoveryPlanRequestStatus.NeedMoreInformation;
        MarkUpdated(request, utcNow);

        AddRequestEvent(
            request,
            RecoveryPlanRequestEventType.MoreInformationRequested,
            RecoveryPlanRequestStatus.InReview,
            request.Status,
            null,
            doctor.Id,
            reason,
            utcNow);
        AddOutboxMessage(request, RecoveryPlanOutboxEventTypes.MoreInformationRequested, utcNow);

        return RecoveryPlanErrorCode.None;
    }

    private async Task<RecoveryPlanErrorCode> RejectRequestAsync(
        RecoveryPlanRequest request,
        Doctor doctor,
        Guid doctorUserId,
        string reasonCode,
        string reason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (request.Status == RecoveryPlanRequestStatus.Rejected)
        {
            return RecoveryPlanErrorCode.None;
        }

        if (!CanReject(request.Status))
        {
            return RecoveryPlanErrorCode.InvalidRequestState;
        }

        var quotaId = await GetQuotaIdAsync(request.UserSubscriptionUsageId, cancellationToken);
        var quotaReleased = await _quota.ReleaseAsync(
            request.UserSubscriptionUsageId,
            request.UserSubscriptionId,
            quotaId,
            request.Id,
            doctorUserId,
            BuildRejectQuotaReleaseKey(request.Id),
            utcNow,
            cancellationToken);
        if (!quotaReleased)
        {
            return RecoveryPlanErrorCode.QuotaMutationFailed;
        }

        var previousStatus = request.Status;
        request.Status = RecoveryPlanRequestStatus.Rejected;
        request.RejectedAt = utcNow;
        request.RejectionReasonCode = reasonCode;
        request.RejectionReason = reason;
        request.AssignmentExpiresAt = null;
        MarkUpdated(request, utcNow);

        AddRequestEvent(
            request,
            RecoveryPlanRequestEventType.Rejected,
            previousStatus,
            request.Status,
            null,
            doctor.Id,
            RejectedByDoctorEventReason,
            utcNow);
        AddOutboxMessage(request, RecoveryPlanOutboxEventTypes.Rejected, utcNow);

        return RecoveryPlanErrorCode.None;
    }

    private Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> UserTransitionAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken,
        Func<RecoveryPlanRequest, Guid, DateTime, RecoveryPlanErrorCode> transition) =>
        UserTransitionAsync(
            userId,
            requestId,
            cancellationToken,
            (request, actorUserId, utcNow, _) =>
                Task.FromResult(transition(request, actorUserId, utcNow)));

    private async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> UserTransitionAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken,
        Func<RecoveryPlanRequest, Guid, DateTime, CancellationToken, Task<RecoveryPlanErrorCode>> transition)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var request = await _uow.RecoveryPlanRequests.GetByIdForUpdateAsync(requestId, cancellationToken);
            if (request is null || request.UserId != userId)
            {
                return await RollbackFailureAsync(RecoveryPlanErrorCode.NotFound);
            }

            var originalVersion = request.Version;
            var error = await transition(request, userId, DateTime.UtcNow, cancellationToken);
            if (error != RecoveryPlanErrorCode.None)
            {
                return await RollbackFailureAsync(error);
            }

            var isReplay = request.Version == originalVersion;
            if (isReplay)
            {
                await RollbackAsync();
            }
            else
            {
                await _uow.SaveChangesAsync(cancellationToken);
                await _uow.CommitTransactionAsync(cancellationToken);
            }

            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request), isReplay);
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    private Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> DoctorTransitionAsync(
        Guid doctorUserId,
        Guid requestId,
        CancellationToken cancellationToken,
        Func<RecoveryPlanRequest, Doctor, DateTime, RecoveryPlanErrorCode> transition) =>
        DoctorTransitionAsync(
            doctorUserId,
            requestId,
            cancellationToken,
            (request, doctor, utcNow, _) => Task.FromResult(transition(request, doctor, utcNow)));

    private async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> DoctorTransitionAsync(
        Guid doctorUserId,
        Guid requestId,
        CancellationToken cancellationToken,
        Func<RecoveryPlanRequest, Doctor, DateTime, CancellationToken, Task<RecoveryPlanErrorCode>> transition)
    {
        var doctor = await _uow.RecoveryPlanRequests.GetDoctorByUserIdAsync(doctorUserId, cancellationToken);
        if (doctor is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                RecoveryPlanErrorCode.DoctorProfileNotFound);
        }

        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var request = await _uow.RecoveryPlanRequests.GetByIdForUpdateAsync(requestId, cancellationToken);
            if (request is null || request.AssignedDoctorId != doctor.Id)
            {
                return await RollbackFailureAsync(RecoveryPlanErrorCode.NotFound);
            }

            var originalVersion = request.Version;
            var error = await transition(request, doctor, DateTime.UtcNow, cancellationToken);
            if (error != RecoveryPlanErrorCode.None)
            {
                return await RollbackFailureAsync(error);
            }

            var isReplay = request.Version == originalVersion;
            if (isReplay)
            {
                await RollbackAsync();
            }
            else
            {
                await _uow.SaveChangesAsync(cancellationToken);
                await _uow.CommitTransactionAsync(cancellationToken);
            }

            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request), isReplay);
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    private async Task<Guid> GetQuotaIdAsync(Guid usageId, CancellationToken cancellationToken)
    {
        var usage = await _uow.QuotaUsages.GetByIdAsync(usageId, cancellationToken);
        if (usage is null)
        {
            throw new InvalidOperationException("Recovery plan request usage could not be resolved.");
        }

        return usage.QuotaId;
    }

    private async Task<RecoveryPlanRequest?> LoadIdempotentReplayAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var log = await _uow.QuotaUsages.GetLogByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (log?.ReferenceId is not Guid requestId)
        {
            return null;
        }

        var request = await _uow.RecoveryPlanRequests.GetByIdAsync(requestId, cancellationToken);
        if (request is null || request.UserId != userId)
        {
            return null;
        }

        return request;
    }

    private void AddRequestEvent(
        RecoveryPlanRequest request,
        RecoveryPlanRequestEventType eventType,
        RecoveryPlanRequestStatus? previousStatus,
        RecoveryPlanRequestStatus? currentStatus,
        Guid? actorUserId,
        Guid? actorDoctorId,
        string? reason,
        DateTime utcNow) =>
        _uow.RecoveryPlanRequests.AddEvent(new RecoveryPlanRequestEvent
        {
            Id = Guid.NewGuid(),
            RecoveryPlanRequestId = request.Id,
            EventType = eventType,
            FromStatus = previousStatus,
            ToStatus = currentStatus,
            ActorUserId = actorUserId,
            ActorDoctorId = actorDoctorId,
            Reason = reason,
            CreatedAt = utcNow
        });

    private void AddOutboxMessage(
        RecoveryPlanRequest request,
        string eventType,
        DateTime utcNow) =>
        _uow.RecoveryPlanRequests.AddOutbox(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            AggregateType = RecoveryPlanOutboxEventTypes.AggregateType,
            AggregateId = request.Id,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = utcNow,
            PayloadJson = JsonSerializer.Serialize(new
            {
                RequestId = request.Id,
                request.UserId,
                DiseaseGroup = request.DiseaseGroup.ToString(),
                Status = request.Status.ToString(),
                request.AssignedDoctorId,
                request.RequestedAt,
                TransitionedAt = utcNow
            })
        });

    private static void MarkUpdated(RecoveryPlanRequest request, DateTime utcNow)
    {
        request.Version++;
        request.UpdatedAt = utcNow;
    }

    private static string? EmptyToNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }

    private static string BuildCreateIdempotencyKey(Guid userId, string idempotencyKey)
    {
        var keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey));
        return $"RPR_CREATE:{userId:N}:{Convert.ToHexString(keyHash)}";
    }

    private static string BuildCancelQuotaReleaseKey(Guid requestId) =>
        $"RPR:{requestId}:RELEASE:CANCEL";

    private static string BuildRejectQuotaReleaseKey(Guid requestId) =>
        $"RPR:{requestId}:RELEASE:REJECT";

    private static RecoveryPlanErrorCode ValidateDoctor(Doctor? doctor)
    {
        if (doctor is null)
        {
            return RecoveryPlanErrorCode.DoctorProfileNotFound;
        }

        if (!doctor.IsActive)
        {
            return RecoveryPlanErrorCode.DoctorNotActive;
        }

        if (!doctor.IsAcceptingRecoveryPlanRequests)
        {
            return RecoveryPlanErrorCode.DoctorNotAcceptingRequests;
        }

        return RecoveryPlanErrorCode.None;
    }

    private static bool IsActiveDoctorAssignmentStatus(RecoveryPlanRequestStatus status) =>
        status is RecoveryPlanRequestStatus.Assigned
            or RecoveryPlanRequestStatus.InReview
            or RecoveryPlanRequestStatus.NeedMoreInformation;

    private static bool CanCancel(RecoveryPlanRequestStatus status) =>
        status == RecoveryPlanRequestStatus.WaitingForDoctor ||
        IsActiveDoctorAssignmentStatus(status);

    private static bool CanRelease(RecoveryPlanRequestStatus status) =>
        IsActiveDoctorAssignmentStatus(status);

    private static bool CanReject(RecoveryPlanRequestStatus status) =>
        IsActiveDoctorAssignmentStatus(status);

    private static RecoveryPlanOperationResult<RecoveryPlanRequestResponse>? ClassifyAcceptState(
        RecoveryPlanRequest? request,
        Guid doctorId)
    {
        if (request is null)
        {
            return FailNotFound();
        }

        if (request.AssignedDoctorId == doctorId)
        {
            if (IsActiveDoctorAssignmentStatus(request.Status))
            {
                return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request), true);
            }

            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                RecoveryPlanErrorCode.InvalidRequestState);
        }

        if (request.AssignedDoctorId.HasValue)
        {
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                RecoveryPlanErrorCode.RecoveryPlanRequestAlreadyClaimed);
        }

        if (request.Status == RecoveryPlanRequestStatus.WaitingForDoctor)
        {
            return null;
        }

        return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
            RecoveryPlanErrorCode.InvalidRequestState);
    }

    private static RecoveryPlanRequestResponse Map(RecoveryPlanRequest request) => new()
    {
        Id = request.Id,
        UserId = request.UserId,
        AssignedDoctorId = request.AssignedDoctorId,
        DiseaseGroup = request.DiseaseGroup,
        TreatmentJourneyId = request.TreatmentJourneyId,
        PrimaryLabTestSessionId = request.PrimaryLabTestSessionId,
        Status = request.Status,
        RequestNote = request.RequestNote,
        RequestedAt = request.RequestedAt,
        AcceptedAt = request.AcceptedAt,
        ReviewStartedAt = request.ReviewStartedAt,
        AssignmentExpiresAt = request.AssignmentExpiresAt,
        RejectedAt = request.RejectedAt,
        CancelledAt = request.CancelledAt,
        RejectionReasonCode = request.RejectionReasonCode,
        RejectionReason = request.RejectionReason,
        Version = request.Version
    };

    private static OpenRecoveryPlanRequestResponse MapOpen(RecoveryPlanRequest request) => new()
    {
        Id = request.Id,
        DiseaseGroup = request.DiseaseGroup,
        Status = request.Status,
        RequestedAt = request.RequestedAt
    };

    private static PagedResponse<TOutput> ToPage<TOutput>(
        PagedResult<RecoveryPlanRequest> page,
        Func<RecoveryPlanRequest, TOutput> map) => new()
        {
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = (int)Math.Ceiling(page.TotalCount / (double)page.PageSize),
            Items = page.Items.Select(map).ToList()
        };

    private static RecoveryPlanOperationResult<RecoveryPlanRequestResponse> FailNotFound() =>
        RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.NotFound);

    private async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> RollbackFailureAsync(
        RecoveryPlanErrorCode error)
    {
        await RollbackAsync();
        return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(error);
    }

    private Task RollbackAsync() =>
        _uow.RollbackTransactionAsync(CancellationToken.None);

    private static Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> InvalidRequestTask() =>
        Task.FromResult(
            RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                RecoveryPlanErrorCode.InvalidRequest));
}
