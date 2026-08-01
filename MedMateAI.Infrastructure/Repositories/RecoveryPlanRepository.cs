using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class RecoveryPlanRepository : IRecoveryPlanRepository
{
    private readonly ApplicationDbContext _context;

    public RecoveryPlanRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<RecoveryPlan?> GetByIdAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        return _context.RecoveryPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(
                plan =>
                    plan.Id == planId
                    && !plan.IsDeleted
                    && plan.RecoveryPlanRequestId.HasValue,
                cancellationToken);
    }

    public Task<RecoveryPlan?> GetDetailByIdAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        return DetailQuery(_context.RecoveryPlans.AsNoTracking())
            .Include(plan => plan.RecoveryPlanRequest)
            .FirstOrDefaultAsync(
                plan =>
                    plan.Id == planId
                    && !plan.IsDeleted
                    && plan.RecoveryPlanRequestId.HasValue,
                cancellationToken);
    }

    public Task<RecoveryPlan?> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return _context.RecoveryPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(
                plan =>
                    plan.RecoveryPlanRequestId == requestId
                    && !plan.IsDeleted,
                cancellationToken);
    }

    public Task<Guid?> GetRequestIdByPlanIdAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        return _context.RecoveryPlans
            .AsNoTracking()
            .Where(plan =>
                plan.Id == planId
                && !plan.IsDeleted
                && plan.RecoveryPlanRequestId.HasValue)
            .Select(plan => plan.RecoveryPlanRequestId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<RecoveryPlan?> GetByIdForUpdateAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        // Plan lifecycle and aggregate writes require a row lock after the request lock.
        var plans = await _context.RecoveryPlans
            .FromSqlInterpolated($"""
                SELECT *
                FROM "RecoveryPlan"
                WHERE "RecoveryPlanId" = {planId}
                  AND "IsDeleted" = FALSE
                  AND "RecoveryPlanRequestId" IS NOT NULL
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        return plans.SingleOrDefault();
    }

    public async Task<IReadOnlyList<RecoveryPlanCompletionCandidate>>
        GetActiveCompletionCandidatesAsync(
            DateOnly maximumEndDate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        return await _context.RecoveryPlans
            .AsNoTracking()
            .Where(plan =>
                !plan.IsDeleted
                && plan.Status == RecoveryPlanStatus.Active
                && plan.EndDate.HasValue
                && plan.EndDate.Value <= maximumEndDate
                && plan.RecoveryPlanRequestId.HasValue)
            .OrderBy(plan => plan.EndDate)
            .ThenBy(plan => plan.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(plan => new RecoveryPlanCompletionCandidate(
                plan.Id,
                plan.UserId,
                plan.EndDate!.Value,
                _context.Users
                    .Where(user =>
                        user.Id == plan.UserId
                        && !user.IsDeleted)
                    .Select(user => user.TimeZoneId)
                    .SingleOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public Task<RecoveryPlan?> GetTrackedDetailAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        return DetailQuery(_context.RecoveryPlans.AsTracking())
            .FirstOrDefaultAsync(
                plan =>
                    plan.Id == planId
                    && !plan.IsDeleted
                    && plan.RecoveryPlanRequestId.HasValue,
                cancellationToken);
    }

    public async Task<PagedResult<RecoveryPlan>> GetUserPlansPagedAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        RecoveryPlanStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RecoveryPlans
            .AsNoTracking()
            .Where(plan =>
                plan.UserId == userId
                && !plan.IsDeleted
                && plan.RecoveryPlanRequestId.HasValue
                && plan.Status != RecoveryPlanStatus.Draft);

        if (status.HasValue)
        {
            query = query.Where(plan => plan.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var plans = await query
            .OrderByDescending(plan => plan.PublishedAt)
            .ThenByDescending(plan => plan.CreatedAt)
            .ThenBy(plan => plan.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RecoveryPlan>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = plans
        };
    }

    public Task<string?> GetUserTimeZoneIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && !user.IsDeleted)
            .Select(user => user.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<RecoveryPlanClinicalContextData?> GetClinicalContextAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.RecoveryPlanRequests
            .AsNoTracking()
            .Where(currentRequest =>
                currentRequest.Id == requestId
                && !currentRequest.IsDeleted)
            .Select(currentRequest => new
            {
                currentRequest.Id,
                currentRequest.UserId,
                currentRequest.AssignedDoctorId,
                currentRequest.DiseaseGroup,
                currentRequest.Status,
                currentRequest.TreatmentJourneyId,
                currentRequest.PrimaryLabTestSessionId,
                currentRequest.RequestedAt,
                currentRequest.RequestNote
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (request is null)
        {
            return null;
        }

        var patientProfile = await _context.PatientProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.UserId == request.UserId
                && !profile.IsDeleted)
            .Select(profile => new RecoveryPlanPatientProfileData(
                profile.Id,
                profile.Height,
                profile.Weight,
                profile.AllergyNote,
                profile.CreatedAt,
                profile.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        var chronicDiseases = await GetChronicDiseasesAsync(
            patientProfile?.PatientProfileId,
            cancellationToken);
        var primaryLabTest = await GetPrimaryLabTestAsync(
            request.PrimaryLabTestSessionId,
            request.UserId,
            cancellationToken);
        var medications = await GetUserMedicationsAsync(
            request.UserId,
            cancellationToken);
        var treatmentJourney = await GetTreatmentJourneyAsync(
            request.TreatmentJourneyId,
            request.UserId,
            cancellationToken);

        return new RecoveryPlanClinicalContextData(
            request.Id,
            request.UserId,
            request.AssignedDoctorId,
            request.DiseaseGroup,
            request.Status,
            request.TreatmentJourneyId,
            request.PrimaryLabTestSessionId,
            request.RequestedAt,
            request.RequestNote,
            patientProfile,
            chronicDiseases,
            primaryLabTest,
            medications,
            treatmentJourney);
    }

    public void Add(RecoveryPlan plan)
    {
        _context.RecoveryPlans.Add(plan);
    }

    public void AddOutbox(OutboxMessage message)
    {
        _context.OutboxMessages.Add(message);
    }

    private async Task<IReadOnlyList<RecoveryPlanChronicDiseaseData>> GetChronicDiseasesAsync(
        Guid? patientProfileId,
        CancellationToken cancellationToken)
    {
        if (!patientProfileId.HasValue)
        {
            return Array.Empty<RecoveryPlanChronicDiseaseData>();
        }

        return await _context.PatientChronicDiseases
            .AsNoTracking()
            .Where(disease =>
                disease.PatientProfileId == patientProfileId.Value
                && !disease.IsDeleted)
            .OrderBy(disease => disease.DiseaseName)
            .ThenBy(disease => disease.Id)
            .Select(disease => new RecoveryPlanChronicDiseaseData(
                disease.DiseaseName,
                disease.From,
                disease.To,
                disease.Note))
            .ToListAsync(cancellationToken);
    }

    private async Task<RecoveryPlanLabTestData?> GetPrimaryLabTestAsync(
        Guid? testSessionId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!testSessionId.HasValue)
        {
            return null;
        }

        var session = await _context.LabTestSessions
            .AsNoTracking()
            .Where(currentSession =>
                currentSession.Id == testSessionId.Value
                && currentSession.UserId == userId
                && !currentSession.IsDeleted)
            .Select(currentSession => new
            {
                currentSession.Id,
                currentSession.CreatedAt,
                currentSession.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return null;
        }

        var results = await _context.LabTestResultDetails
            .AsNoTracking()
            .Where(result =>
                result.TestSessionId == session.Id
                && !result.IsDeleted
                && result.IndicatorId != null
                && !result.Indicator!.IsDeleted)
            .OrderBy(result => result.Indicator!.Symbol)
            .ThenBy(result => result.IndicatorId)
            .Select(result => new RecoveryPlanLabResultData(
                result.IndicatorId!.Value,
                result.Indicator!.Symbol,
                result.Indicator.FullName,
                result.Indicator.Unit,
                result.UserValue,
                result.Status.ToString(),
                result.Indicator.MinReference,
                result.Indicator.MaxReference))
            .ToListAsync(cancellationToken);

        return new RecoveryPlanLabTestData(
            session.Id,
            session.CreatedAt,
            session.UpdatedAt,
            results);
    }

    private Task<List<RecoveryPlanUserMedicationData>> GetUserMedicationsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _context.UserMedications
            .AsNoTracking()
            .Where(medication =>
                medication.UserId == userId
                && !medication.IsDeleted
                && medication.SourceType == UserMedicationSourceType.PatientReported
                && medication.TreatmentJourneyId == null)
            .OrderBy(medication => medication.MedicineName)
            .ThenBy(medication => medication.Id)
            .Select(medication => new RecoveryPlanUserMedicationData(
                medication.Id,
                medication.MedicineName,
                medication.DosageInstruction,
                medication.StartDate,
                medication.EndDate,
                medication.Status,
                medication.SourceType))
            .ToListAsync(cancellationToken);
    }

    private Task<RecoveryPlanTreatmentJourneyData?> GetTreatmentJourneyAsync(
        Guid? treatmentJourneyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!treatmentJourneyId.HasValue)
        {
            return Task.FromResult<RecoveryPlanTreatmentJourneyData?>(null);
        }

        return _context.TreatmentJourneys
            .AsNoTracking()
            .Where(journey =>
                journey.Id == treatmentJourneyId.Value
                && journey.UserId == userId
                && !journey.IsDeleted)
            .Select(journey => new RecoveryPlanTreatmentJourneyData(
                journey.Id,
                journey.Title,
                journey.DiagnosisSummary,
                journey.StartDate,
                journey.EndDate,
                journey.Status,
                journey.ApprovedAt,
                journey.ApprovalStatus,
                journey.ApprovalNote))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<RecoveryPlan> DetailQuery(IQueryable<RecoveryPlan> query)
    {
        return query
            .Include(plan => plan.Phases.Where(phase => !phase.IsDeleted))
            .ThenInclude(phase => phase.NutrientTargets.Where(nutrient => !nutrient.IsDeleted))
            .ThenInclude(nutrient => nutrient.FoodSources.Where(food => !food.IsDeleted));
    }

    private void EnsureActiveTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Recovery plan locking and tracked aggregate writes require an active database transaction.");
        }
    }
}
