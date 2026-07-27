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
        Guid userId, string rawKey, CreateRecoveryPlanRequest input, CancellationToken token)
    {
        rawKey = rawKey?.Trim() ?? string.Empty;
        if (rawKey.Length is < 1 or > 100)
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.IdempotencyKeyInvalid);
        var note = input.RequestNote?.Trim();
        if (!Enum.IsDefined(input.DiseaseGroup) || note?.Length > 2000)
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.InvalidRequest);
        if (input.TreatmentJourneyId.HasValue &&
            !await _uow.RecoveryPlanRequests.IsOwnedTreatmentJourneyAsync(input.TreatmentJourneyId.Value, userId, token))
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.NotFound);
        if (input.PrimaryLabTestSessionId.HasValue &&
            !await _uow.RecoveryPlanRequests.IsOwnedLabSessionAsync(input.PrimaryLabTestSessionId.Value, userId, token))
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.NotFound);

        var key = CreateScopedKey(userId, rawKey);
        var existing = await ReplayAsync(userId, key, token);
        if (existing is not null) return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(existing), true);

        var requestId = Guid.NewGuid();
        await _uow.BeginTransactionAsync(token);
        try
        {
            var now = DateTime.UtcNow;
            var usageResult = await _quota.ResolveUsageAsync(userId, now, token);
            if (!usageResult.Success || usageResult.Data is null)
            {
                await RollbackAsync();
                return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(usageResult.Error);
            }
            var usage = usageResult.Data;
            if (!await _quota.ReserveAsync(usage.Id, usage.UserSubscriptionId, usage.QuotaId,
                    requestId, userId, key, now, token))
            {
                await RollbackAsync();
                existing = await ReplayAsync(userId, key, token);
                return existing is not null
                    ? RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(existing), true)
                    : RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.RecoveryPlanQuotaExhausted);
            }
            var request = new RecoveryPlanRequest
            {
                Id = requestId, UserId = userId, DiseaseGroup = input.DiseaseGroup,
                TreatmentJourneyId = input.TreatmentJourneyId, PrimaryLabTestSessionId = input.PrimaryLabTestSessionId,
                UserSubscriptionId = usage.UserSubscriptionId, UserSubscriptionUsageId = usage.Id,
                Status = RecoveryPlanRequestStatus.WaitingForDoctor, RequestNote = EmptyToNull(note),
                RequestedAt = now, CreatedAt = now, Version = 0
            };
            _uow.RecoveryPlanRequests.Add(request);
            AddEvent(request, RecoveryPlanRequestEventType.Created, null, request.Status, userId, null, null, now);
            AddEvent(request, RecoveryPlanRequestEventType.QuotaReserved, request.Status, request.Status, userId, null, null, now);
            AddOutbox(request, RecoveryPlanOutboxEventTypes.Created, now);
            await _uow.SaveChangesAsync(token);
            await _uow.CommitTransactionAsync(token);
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public async Task<RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>> GetMineAsync(
        Guid userId, PaginationQuery page, RecoveryPlanRequestStatus? status, CancellationToken token)
    {
        var result = await _uow.RecoveryPlanRequests.GetByUserPagedAsync(userId, page.PageNumber, page.PageSize, status, token);
        return RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>.Ok(ToPage(result, Map));
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> GetDetailAsync(
        Guid userId, bool isDoctor, bool isAdmin, Guid id, CancellationToken token)
    {
        var request = await _uow.RecoveryPlanRequests.GetByIdAsync(id, false, token);
        if (request is null) return FailNotFound();
        if (request.UserId != userId && !isAdmin)
        {
            if (!isDoctor) return FailNotFound();
            var doctor = await _uow.RecoveryPlanRequests.GetDoctorByUserIdAsync(userId, token);
            if (doctor is null || request.AssignedDoctorId != doctor.Id) return FailNotFound();
        }
        return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request));
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> CancelAsync(Guid userId, Guid id, CancellationToken token) =>
        UserTransitionAsync(userId, id, token, async (request, now) =>
        {
            if (request.Status == RecoveryPlanRequestStatus.Cancelled)
            {
                return RecoveryPlanErrorCode.None;
            }

            if (request.Status is not (RecoveryPlanRequestStatus.WaitingForDoctor or RecoveryPlanRequestStatus.Assigned
                or RecoveryPlanRequestStatus.InReview or RecoveryPlanRequestStatus.NeedMoreInformation))
            {
                return RecoveryPlanErrorCode.InvalidRequestState;
            }

            var released = await _quota.ReleaseAsync(request.UserSubscriptionUsageId, request.UserSubscriptionId,
                await GetQuotaIdAsync(request.UserSubscriptionUsageId, token), request.Id, userId,
                $"RPR:{request.Id}:RELEASE:CANCEL", now, token);
            if (!released)
            {
                return RecoveryPlanErrorCode.QuotaMutationFailed;
            }

            var from = request.Status;
            request.Status = RecoveryPlanRequestStatus.Cancelled;
            request.CancelledAt = now;
            request.AssignmentExpiresAt = null;
            Touch(request, now);
            AddEvent(request, RecoveryPlanRequestEventType.Cancelled, from, request.Status, userId, null, null, now);
            AddOutbox(request, RecoveryPlanOutboxEventTypes.Cancelled, now);
            return RecoveryPlanErrorCode.None;
        });

    public Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> ProvideInformationAsync(
        Guid userId, Guid id, string information, CancellationToken token)
    {
        var value = information?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > 2000) return Task.FromResult(
            RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.InvalidRequest));
        return UserTransitionAsync(userId, id, token, (request, now) =>
        {
            if (request.Status != RecoveryPlanRequestStatus.NeedMoreInformation)
            {
                return Task.FromResult(RecoveryPlanErrorCode.InvalidRequestState);
            }

            request.RequestNote = value;
            request.Status = RecoveryPlanRequestStatus.InReview;
            Touch(request, now);
            AddEvent(request, RecoveryPlanRequestEventType.Reopened, RecoveryPlanRequestStatus.NeedMoreInformation,
                request.Status, userId, null, "Additional information provided by request owner.", now);
            AddOutbox(request, RecoveryPlanOutboxEventTypes.InformationProvided, now);
            return Task.FromResult(RecoveryPlanErrorCode.None);
        });
    }

    public async Task<RecoveryPlanOperationResult<PagedResponse<OpenRecoveryPlanRequestResponse>>> GetOpenAsync(
        Guid doctorUserId, PaginationQuery page, RecoveryPlanDiseaseGroup? group, CancellationToken token)
    {
        var doctor = await _uow.RecoveryPlanRequests.GetDoctorByUserIdAsync(doctorUserId, token);
        var error = ValidateDoctor(doctor);
        if (error != RecoveryPlanErrorCode.None)
            return RecoveryPlanOperationResult<PagedResponse<OpenRecoveryPlanRequestResponse>>.Fail(error);
        var result = await _uow.RecoveryPlanRequests.GetOpenPagedAsync(page.PageNumber, page.PageSize, group, token);
        return RecoveryPlanOperationResult<PagedResponse<OpenRecoveryPlanRequestResponse>>.Ok(ToPage(result, MapOpen));
    }

    public async Task<RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>> GetDoctorMineAsync(
        Guid doctorUserId, PaginationQuery page, RecoveryPlanRequestStatus? status, CancellationToken token)
    {
        var doctor = await _uow.RecoveryPlanRequests.GetDoctorByUserIdAsync(doctorUserId, token);
        if (doctor is null) return RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>.Fail(RecoveryPlanErrorCode.DoctorProfileNotFound);
        var result = await _uow.RecoveryPlanRequests.GetAssignedPagedAsync(doctor.Id, page.PageNumber, page.PageSize, status, token);
        return RecoveryPlanOperationResult<PagedResponse<RecoveryPlanRequestResponse>>.Ok(ToPage(result, Map));
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> AcceptAsync(Guid doctorUserId, Guid id, CancellationToken token)
    {
        await _uow.BeginTransactionAsync(token);
        try
        {
            var doctor = await _uow.RecoveryPlanRequests.GetDoctorByUserIdForUpdateAsync(doctorUserId, token);
            var error = ValidateDoctor(doctor);
            if (error != RecoveryPlanErrorCode.None)
            {
                return await RollbackFailure(error);
            }

            var existing = await _uow.RecoveryPlanRequests.GetByIdAsync(id, false, token);
            var existingResult = ClassifyAcceptState(existing, doctor!.Id);
            if (existingResult is not null)
            {
                await RollbackAsync();
                return existingResult;
            }

            var count = await _uow.RecoveryPlanRequests.CountActiveAssignmentsAsync(doctor!.Id, token);
            if (doctor.MaxConcurrentRecoveryPlanRequests.HasValue && count >= doctor.MaxConcurrentRecoveryPlanRequests.Value)
            {
                return await RollbackFailure(RecoveryPlanErrorCode.DoctorCapacityReached);
            }

            var now = DateTime.UtcNow;
            var request = await _uow.RecoveryPlanRequests.TryAcceptAsync(id, doctor.Id, now,
                now.AddMinutes(_options.AssignmentTimeoutMinutes), token);
            if (request is null)
            {
                var current = await _uow.RecoveryPlanRequests.GetByIdAsync(id, false, token);
                await RollbackAsync();
                return ClassifyAcceptState(current, doctor.Id)
                    ?? RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                        RecoveryPlanErrorCode.InvalidRequestState);
            }

            AddEvent(request, RecoveryPlanRequestEventType.Accepted, RecoveryPlanRequestStatus.WaitingForDoctor,
                RecoveryPlanRequestStatus.Assigned, null, doctor.Id, null, now);
            AddOutbox(request, RecoveryPlanOutboxEventTypes.Claimed, now);
            await _uow.SaveChangesAsync(token);
            await _uow.CommitTransactionAsync(token);
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> StartReviewAsync(Guid doctorUserId, Guid id, CancellationToken token) =>
        DoctorTransitionAsync(doctorUserId, id, token, (request, doctor, now) =>
        {
            if (request.Status == RecoveryPlanRequestStatus.InReview) return RecoveryPlanErrorCode.None;
            if (request.Status != RecoveryPlanRequestStatus.Assigned) return RecoveryPlanErrorCode.InvalidRequestState;
            if (request.AssignmentExpiresAt <= now) return RecoveryPlanErrorCode.AssignmentExpired;
            request.Status = RecoveryPlanRequestStatus.InReview;
            request.ReviewStartedAt = now;
            request.AssignmentExpiresAt = null;
            Touch(request, now);
            AddEvent(request, RecoveryPlanRequestEventType.ReviewStarted, RecoveryPlanRequestStatus.Assigned,
                request.Status, null, doctor.Id, null, now);
            AddOutbox(request, RecoveryPlanOutboxEventTypes.ReviewStarted, now);
            return RecoveryPlanErrorCode.None;
        });

    public Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> ReleaseAsync(
        Guid doctorUserId, Guid id, string? reason, CancellationToken token)
    {
        var value = EmptyToNull(reason?.Trim());
        if (value?.Length > 2000) return InvalidTask();
        return DoctorTransitionAsync(doctorUserId, id, token, (request, doctor, now) =>
        {
            if (request.Status is not (RecoveryPlanRequestStatus.Assigned or RecoveryPlanRequestStatus.InReview
                or RecoveryPlanRequestStatus.NeedMoreInformation)) return RecoveryPlanErrorCode.InvalidRequestState;
            var from = request.Status;
            request.Status = RecoveryPlanRequestStatus.WaitingForDoctor;
            request.AssignedDoctorId = null;
            request.AcceptedAt = null;
            request.ReviewStartedAt = null;
            request.AssignmentExpiresAt = null;
            Touch(request, now);
            AddEvent(request, RecoveryPlanRequestEventType.Released, from, request.Status, null, doctor.Id, value, now);
            AddOutbox(request, RecoveryPlanOutboxEventTypes.Released, now);
            return RecoveryPlanErrorCode.None;
        });
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> RequestInformationAsync(
        Guid doctorUserId, Guid id, string reason, CancellationToken token)
    {
        var value = reason?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > 2000) return InvalidTask();
        return DoctorTransitionAsync(doctorUserId, id, token, (request, doctor, now) =>
        {
            if (request.Status == RecoveryPlanRequestStatus.NeedMoreInformation) return RecoveryPlanErrorCode.None;
            if (request.Status != RecoveryPlanRequestStatus.InReview) return RecoveryPlanErrorCode.InvalidRequestState;
            request.Status = RecoveryPlanRequestStatus.NeedMoreInformation;
            Touch(request, now);
            AddEvent(request, RecoveryPlanRequestEventType.MoreInformationRequested, RecoveryPlanRequestStatus.InReview,
                request.Status, null, doctor.Id, value, now);
            AddOutbox(request, RecoveryPlanOutboxEventTypes.MoreInformationRequested, now);
            return RecoveryPlanErrorCode.None;
        });
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> RejectAsync(
        Guid doctorUserId, Guid id, string code, string reason, CancellationToken token)
    {
        code = code?.Trim() ?? string.Empty;
        reason = reason?.Trim() ?? string.Empty;
        if (code.Length is < 1 or > 100 || reason.Length is < 1 or > 2000) return await InvalidTask();
        return await DoctorTransitionAsync(doctorUserId, id, token, async (request, doctor, now) =>
        {
            if (request.Status == RecoveryPlanRequestStatus.Rejected) return RecoveryPlanErrorCode.None;
            if (request.Status is not (RecoveryPlanRequestStatus.Assigned or RecoveryPlanRequestStatus.InReview
                or RecoveryPlanRequestStatus.NeedMoreInformation)) return RecoveryPlanErrorCode.InvalidRequestState;
            var quotaId = await GetQuotaIdAsync(request.UserSubscriptionUsageId, token);
            if (!await _quota.ReleaseAsync(request.UserSubscriptionUsageId, request.UserSubscriptionId, quotaId,
                    request.Id, doctorUserId, $"RPR:{request.Id}:RELEASE:REJECT", now, token))
                return RecoveryPlanErrorCode.QuotaMutationFailed;
            var from = request.Status;
            request.Status = RecoveryPlanRequestStatus.Rejected;
            request.RejectedAt = now;
            request.RejectionReasonCode = code;
            request.RejectionReason = reason;
            request.AssignmentExpiresAt = null;
            Touch(request, now);
            AddEvent(request, RecoveryPlanRequestEventType.Rejected, from, request.Status, null, doctor.Id,
                "Recovery plan request rejected by assigned doctor.", now);
            AddOutbox(request, RecoveryPlanOutboxEventTypes.Rejected, now);
            return RecoveryPlanErrorCode.None;
        });
    }

    private async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> UserTransitionAsync(
        Guid userId, Guid id, CancellationToken token,
        Func<RecoveryPlanRequest, DateTime, Task<RecoveryPlanErrorCode>> mutate)
    {
        await _uow.BeginTransactionAsync(token);
        try
        {
            var request = await _uow.RecoveryPlanRequests.GetByIdForUpdateAsync(id, token);
            if (request is null || request.UserId != userId)
            {
                return await RollbackFailure(RecoveryPlanErrorCode.NotFound);
            }

            var originalVersion = request.Version;
            var error = await mutate(request, DateTime.UtcNow);
            if (error != RecoveryPlanErrorCode.None)
            {
                return await RollbackFailure(error);
            }

            if (request.Version != originalVersion)
            {
                await _uow.SaveChangesAsync(token);
                await _uow.CommitTransactionAsync(token);
            }
            else
            {
                await RollbackAsync();
            }

            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request), request.Version == originalVersion);
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    private Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> DoctorTransitionAsync(
        Guid userId, Guid id, CancellationToken token,
        Func<RecoveryPlanRequest, Doctor, DateTime, RecoveryPlanErrorCode> mutate) =>
        DoctorTransitionAsync(userId, id, token, (r, d, n) => Task.FromResult(mutate(r, d, n)));

    private async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> DoctorTransitionAsync(
        Guid userId, Guid id, CancellationToken token,
        Func<RecoveryPlanRequest, Doctor, DateTime, Task<RecoveryPlanErrorCode>> mutate)
    {
        var doctor = await _uow.RecoveryPlanRequests.GetDoctorByUserIdAsync(userId, token);
        if (doctor is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                RecoveryPlanErrorCode.DoctorProfileNotFound);
        }

        await _uow.BeginTransactionAsync(token);
        try
        {
            var request = await _uow.RecoveryPlanRequests.GetByIdForUpdateAsync(id, token);
            if (request is null || request.AssignedDoctorId != doctor.Id)
            {
                return await RollbackFailure(RecoveryPlanErrorCode.NotFound);
            }

            var originalVersion = request.Version;
            var error = await mutate(request, doctor, DateTime.UtcNow);
            if (error != RecoveryPlanErrorCode.None)
            {
                return await RollbackFailure(error);
            }

            if (request.Version != originalVersion)
            {
                await _uow.SaveChangesAsync(token);
                await _uow.CommitTransactionAsync(token);
            }
            else
            {
                await RollbackAsync();
            }

            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request), request.Version == originalVersion);
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    private async Task<Guid> GetQuotaIdAsync(Guid usageId, CancellationToken token)
    {
        var usage = await _uow.QuotaUsages.GetByIdAsync(usageId, token);
        return usage?.QuotaId
            ?? throw new InvalidOperationException("Recovery plan request usage could not be resolved.");
    }

    private async Task<RecoveryPlanRequest?> ReplayAsync(Guid userId, string key, CancellationToken token)
    {
        var log = await _uow.QuotaUsages.GetLogByIdempotencyKeyAsync(key, token);
        return log?.ReferenceId is Guid id
            ? await _uow.RecoveryPlanRequests.GetByIdAsync(id, false, token) is { UserId: var owner } request && owner == userId ? request : null
            : null;
    }

    private void AddEvent(RecoveryPlanRequest request, RecoveryPlanRequestEventType type,
        RecoveryPlanRequestStatus? from, RecoveryPlanRequestStatus? to, Guid? userId, Guid? doctorId,
        string? reason, DateTime now) =>
        _uow.RecoveryPlanRequests.AddEvent(new RecoveryPlanRequestEvent
        {
            Id = Guid.NewGuid(), RecoveryPlanRequestId = request.Id, EventType = type,
            FromStatus = from, ToStatus = to, ActorUserId = userId, ActorDoctorId = doctorId,
            Reason = reason, CreatedAt = now
        });

    private void AddOutbox(RecoveryPlanRequest request, string eventType, DateTime now) =>
        _uow.RecoveryPlanRequests.AddOutbox(new OutboxMessage
        {
            Id = Guid.NewGuid(), EventType = eventType,
            AggregateType = RecoveryPlanOutboxEventTypes.AggregateType,
            AggregateId = request.Id, Status = OutboxMessageStatus.Pending, CreatedAt = now,
            PayloadJson = JsonSerializer.Serialize(new
            {
                RequestId = request.Id,
                request.UserId,
                DiseaseGroup = request.DiseaseGroup.ToString(),
                Status = request.Status.ToString(),
                request.AssignedDoctorId, request.RequestedAt, TransitionedAt = now
            })
        });

    private static void Touch(RecoveryPlanRequest request, DateTime now)
    {
        request.Version++;
        request.UpdatedAt = now;
    }
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string CreateScopedKey(Guid userId, string raw) =>
        $"RPR_CREATE:{userId:N}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))}";
    private static RecoveryPlanErrorCode ValidateDoctor(Doctor? doctor) =>
        doctor is null ? RecoveryPlanErrorCode.DoctorProfileNotFound :
        !doctor.IsActive ? RecoveryPlanErrorCode.DoctorNotActive :
        !doctor.IsAcceptingRecoveryPlanRequests ? RecoveryPlanErrorCode.DoctorNotAcceptingRequests :
        RecoveryPlanErrorCode.None;

    private static RecoveryPlanOperationResult<RecoveryPlanRequestResponse>? ClassifyAcceptState(
        RecoveryPlanRequest? request, Guid doctorId)
    {
        if (request is null)
        {
            return FailNotFound();
        }

        if (request.AssignedDoctorId == doctorId)
        {
            return request.Status is RecoveryPlanRequestStatus.Assigned
                or RecoveryPlanRequestStatus.InReview
                or RecoveryPlanRequestStatus.NeedMoreInformation
                ? RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Ok(Map(request), true)
                : RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                    RecoveryPlanErrorCode.InvalidRequestState);
        }

        if (request.AssignedDoctorId.HasValue)
        {
            return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                RecoveryPlanErrorCode.RecoveryPlanRequestAlreadyClaimed);
        }

        return request.Status == RecoveryPlanRequestStatus.WaitingForDoctor
            ? null
            : RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(
                RecoveryPlanErrorCode.InvalidRequestState);
    }
    private static RecoveryPlanRequestResponse Map(RecoveryPlanRequest x) => new()
    {
        Id = x.Id, UserId = x.UserId, AssignedDoctorId = x.AssignedDoctorId, DiseaseGroup = x.DiseaseGroup,
        TreatmentJourneyId = x.TreatmentJourneyId, PrimaryLabTestSessionId = x.PrimaryLabTestSessionId,
        Status = x.Status, RequestNote = x.RequestNote, RequestedAt = x.RequestedAt, AcceptedAt = x.AcceptedAt,
        ReviewStartedAt = x.ReviewStartedAt, AssignmentExpiresAt = x.AssignmentExpiresAt,
        RejectedAt = x.RejectedAt, CancelledAt = x.CancelledAt, RejectionReasonCode = x.RejectionReasonCode,
        RejectionReason = x.RejectionReason, Version = x.Version
    };
    private static OpenRecoveryPlanRequestResponse MapOpen(RecoveryPlanRequest x) => new()
        { Id = x.Id, DiseaseGroup = x.DiseaseGroup, Status = x.Status, RequestedAt = x.RequestedAt };
    private static PagedResponse<TOut> ToPage<TOut>(PagedResult<RecoveryPlanRequest> page, Func<RecoveryPlanRequest, TOut> map) => new()
    {
        PageNumber = page.PageNumber, PageSize = page.PageSize, TotalCount = page.TotalCount,
        TotalPages = (int)Math.Ceiling(page.TotalCount / (double)page.PageSize), Items = page.Items.Select(map).ToList()
    };
    private static RecoveryPlanOperationResult<RecoveryPlanRequestResponse> FailNotFound() =>
        RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.NotFound);
    private async Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> RollbackFailure(
        RecoveryPlanErrorCode error)
    {
        await RollbackAsync();
        return RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(error);
    }

    private Task RollbackAsync() =>
        _uow.RollbackTransactionAsync(CancellationToken.None);
    private static Task<RecoveryPlanOperationResult<RecoveryPlanRequestResponse>> InvalidTask() =>
        Task.FromResult(RecoveryPlanOperationResult<RecoveryPlanRequestResponse>.Fail(RecoveryPlanErrorCode.InvalidRequest));
}
