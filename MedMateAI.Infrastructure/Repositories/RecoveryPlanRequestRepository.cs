using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class RecoveryPlanRequestRepository : IRecoveryPlanRequestRepository
{
    private readonly ApplicationDbContext _context;

    public RecoveryPlanRequestRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecoveryPlanRequest?> GetByIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecoveryPlanRequests
            .AsNoTracking()
            .Where(request => !request.IsDeleted && request.Id == requestId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RecoveryPlanRequest?> GetByIdForUpdateAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        // A row lock is required so state validation and the following transition stay atomic.
        var requests = await _context.RecoveryPlanRequests
            .FromSqlInterpolated($"""
                SELECT *
                FROM "RecoveryPlanRequest"
                WHERE "RecoveryPlanRequestId" = {requestId}
                  AND "IsDeleted" = FALSE
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        return requests.SingleOrDefault();
    }

    public async Task<IReadOnlyList<Guid>> GetExpiredAssignmentIdsAsync(
        DateTime utcNow,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecoveryPlanRequests
            .AsNoTracking()
            .Where(request =>
                !request.IsDeleted
                && request.Status == RecoveryPlanRequestStatus.Assigned
                && request.AssignedDoctorId.HasValue
                && request.AssignmentExpiresAt.HasValue
                && request.AssignmentExpiresAt.Value <= utcNow)
            .OrderBy(request => request.AssignmentExpiresAt)
            .ThenBy(request => request.Id)
            .Select(request => request.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<RecoveryPlanRequest>> GetOpenPagedAsync(
        int pageNumber,
        int pageSize,
        RecoveryPlanDiseaseGroup? diseaseGroup,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RecoveryPlanRequests
            .AsNoTracking()
            .Where(request =>
                !request.IsDeleted
                && request.Status == RecoveryPlanRequestStatus.WaitingForDoctor
                && request.AssignedDoctorId == null);

        if (diseaseGroup.HasValue)
        {
            query = query.Where(request => request.DiseaseGroup == diseaseGroup.Value);
        }

        return await ToPagedResultAsync(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<PagedResult<RecoveryPlanRequest>> GetByUserPagedAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        RecoveryPlanRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RecoveryPlanRequests
            .AsNoTracking()
            .Where(request => !request.IsDeleted && request.UserId == userId);

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        return await ToPagedResultAsync(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<DoctorRecoveryPlanRequestData?> GetAssignedToDoctorByIdAsync(
        Guid doctorId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var requests = _context.RecoveryPlanRequests
            .AsNoTracking()
            .Where(request =>
                !request.IsDeleted
                && request.Id == requestId
                && request.AssignedDoctorId == doctorId);

        var request = await ProjectDoctorRequests(requests)
            .SingleOrDefaultAsync(cancellationToken);
        if (request is null)
        {
            return null;
        }

        var planSummaries = await GetActivePlanSummariesAsync(
            new[] { request.Id },
            cancellationToken);

        return AttachPlanSummary(request, planSummaries);
    }

    public async Task<PagedResult<DoctorRecoveryPlanRequestData>> GetAssignedToDoctorPagedAsync(
        Guid doctorId,
        int pageNumber,
        int pageSize,
        RecoveryPlanRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RecoveryPlanRequests
            .AsNoTracking()
            .Where(request =>
                !request.IsDeleted
                && request.AssignedDoctorId == doctorId);

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageRequests = await ProjectDoctorRequests(query)
            .OrderBy(request => request.RequestedAt)
            .ThenBy(request => request.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var planSummaries = await GetActivePlanSummariesAsync(
            pageRequests.Select(request => request.Id).ToArray(),
            cancellationToken);
        var items = pageRequests
            .Select(request => AttachPlanSummary(request, planSummaries))
            .ToList();

        return new PagedResult<DoctorRecoveryPlanRequestData>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<RecoveryPlanRequest?> TryAcceptAsync(
        Guid requestId,
        Guid doctorId,
        DateTime acceptedAt,
        DateTime assignmentExpiresAt,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        var affectedRows = await _context.RecoveryPlanRequests
            .Where(request =>
                request.Id == requestId
                && !request.IsDeleted
                && request.Status == RecoveryPlanRequestStatus.WaitingForDoctor
                && request.AssignedDoctorId == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(request => request.AssignedDoctorId, doctorId)
                    .SetProperty(request => request.Status, RecoveryPlanRequestStatus.Assigned)
                    .SetProperty(request => request.AcceptedAt, acceptedAt)
                    .SetProperty(request => request.AssignmentExpiresAt, assignmentExpiresAt)
                    .SetProperty(request => request.UpdatedAt, utcNow)
                    .SetProperty(request => request.Version, request => request.Version + 1),
                cancellationToken);

        if (affectedRows != 1)
        {
            return null;
        }

        return await GetByIdAsync(requestId, cancellationToken);
    }

    public async Task<Doctor?> GetDoctorByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
            .AsNoTracking()
            .Where(doctor => !doctor.IsDeleted && doctor.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<RecoveryPlanRealtimeDoctorAccessData?> GetRealtimeDoctorAccessAsync(
        Guid doctorUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.Doctors
            .AsNoTracking()
            .Where(doctor =>
                !doctor.IsDeleted
                && doctor.UserId == doctorUserId)
            .Select(doctor => new RecoveryPlanRealtimeDoctorAccessData(
                doctor.Id,
                doctor.IsActive,
                doctor.IsAcceptingRecoveryPlanRequests,
                _context.Users.Any(user =>
                    user.Id == doctorUserId
                    && !user.IsDeleted
                    && user.Status == UserStatus.Confirmed)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Doctor?> GetDoctorByUserIdForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        // Capacity is checked while this doctor row lock is held by the current transaction.
        var doctors = await _context.Doctors
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Doctor"
                WHERE "UserId" = {userId}
                  AND "IsDeleted" = FALSE
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        return doctors.SingleOrDefault();
    }

    public Task<int> CountActiveAssignmentsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        return _context.RecoveryPlanRequests.CountAsync(
            request =>
                !request.IsDeleted
                && request.AssignedDoctorId == doctorId
                && (request.Status == RecoveryPlanRequestStatus.Assigned
                    || request.Status == RecoveryPlanRequestStatus.InReview
                    || request.Status == RecoveryPlanRequestStatus.NeedMoreInformation),
            cancellationToken);
    }

    public Task<bool> IsOwnedTreatmentJourneyAsync(
        Guid treatmentJourneyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.TreatmentJourneys.AnyAsync(
            journey =>
                journey.Id == treatmentJourneyId
                && journey.UserId == userId
                && !journey.IsDeleted,
            cancellationToken);
    }

    public Task<bool> IsOwnedLabSessionAsync(
        Guid labTestSessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.LabTestSessions.AnyAsync(
            session =>
                session.Id == labTestSessionId
                && session.UserId == userId
                && !session.IsDeleted,
            cancellationToken);
    }

    public void Add(RecoveryPlanRequest request)
    {
        _context.RecoveryPlanRequests.Add(request);
    }

    public void AddEvent(RecoveryPlanRequestEvent requestEvent)
    {
        _context.RecoveryPlanRequestEvents.Add(requestEvent);
    }

    public void AddOutbox(OutboxMessage message)
    {
        _context.OutboxMessages.Add(message);
    }

    private static IQueryable<DoctorRecoveryPlanRequestData> ProjectDoctorRequests(
        IQueryable<RecoveryPlanRequest> requests)
    {
        return requests.Select(request => new DoctorRecoveryPlanRequestData(
            request.Id,
            request.UserId,
            request.AssignedDoctorId,
            request.DiseaseGroup,
            request.TreatmentJourneyId,
            request.PrimaryLabTestSessionId,
            request.Status,
            request.RequestNote,
            request.RequestedAt,
            request.AcceptedAt,
            request.ReviewStartedAt,
            request.AssignmentExpiresAt,
            request.RejectedAt,
            request.CancelledAt,
            request.RejectionReasonCode,
            request.RejectionReason,
            request.Version,
            null,
            null));
    }

    private async Task<IReadOnlyDictionary<Guid, (Guid PlanId, RecoveryPlanStatus Status)>>
        GetActivePlanSummariesAsync(
            IReadOnlyCollection<Guid> requestIds,
            CancellationToken cancellationToken)
    {
        if (requestIds.Count == 0)
        {
            return new Dictionary<Guid, (Guid PlanId, RecoveryPlanStatus Status)>();
        }

        var plans = await _context.RecoveryPlans
            .AsNoTracking()
            .Where(plan =>
                !plan.IsDeleted
                && plan.RecoveryPlanRequestId.HasValue
                && requestIds.Contains(plan.RecoveryPlanRequestId.Value))
            .OrderByDescending(plan => plan.CreatedAt)
            .ThenByDescending(plan => plan.Id)
            .Select(plan => new
            {
                RequestId = plan.RecoveryPlanRequestId.GetValueOrDefault(),
                PlanId = plan.Id,
                plan.Status
            })
            .ToListAsync(cancellationToken);

        var summaries = new Dictionary<Guid, (Guid PlanId, RecoveryPlanStatus Status)>();
        foreach (var plan in plans)
        {
            // The filtered unique index guarantees one active plan per request.
            // TryAdd also keeps materialization safe if legacy data violates that invariant.
            summaries.TryAdd(plan.RequestId, (plan.PlanId, plan.Status));
        }

        return summaries;
    }

    private static DoctorRecoveryPlanRequestData AttachPlanSummary(
        DoctorRecoveryPlanRequestData request,
        IReadOnlyDictionary<Guid, (Guid PlanId, RecoveryPlanStatus Status)> planSummaries)
    {
        if (!planSummaries.TryGetValue(request.Id, out var plan))
        {
            return request;
        }

        return request with
        {
            RecoveryPlanId = plan.PlanId,
            RecoveryPlanStatus = plan.Status
        };
    }

    private void EnsureActiveTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Recovery plan request locking and mutations require an active database transaction.");
        }
    }

    private static async Task<PagedResult<RecoveryPlanRequest>> ToPagedResultAsync(
        IQueryable<RecoveryPlanRequest> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(request => request.RequestedAt)
            .ThenBy(request => request.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RecoveryPlanRequest>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }
}
