using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class UserMedicationRepository : IUserMedicationRepository
{
    private const string PatientReportedSourceValue =
        nameof(UserMedicationSourceType.PatientReported);

    private readonly ApplicationDbContext _context;

    public UserMedicationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<UserMedication>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await ReadQuery()
            .Where(medication => medication.UserId == userId)
            .OrderByDescending(medication => medication.CreatedAt)
            .ThenBy(medication => medication.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<UserMedication>> GetByUserIdPagedAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ReadQuery()
            .Where(medication => medication.UserId == userId)
            .OrderByDescending(medication => medication.CreatedAt)
            .ThenBy(medication => medication.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserMedication>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items
        };
    }

    public Task<UserMedication?> GetByIdAsync(
        Guid userId,
        Guid medicationId,
        CancellationToken cancellationToken = default)
    {
        return ReadQuery()
            .SingleOrDefaultAsync(
                medication =>
                    medication.Id == medicationId
                    && medication.UserId == userId,
                cancellationToken);
    }

    public async Task<UserMedication?> GetByIdForUpdateAsync(
        Guid userId,
        Guid medicationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActiveTransaction();

        // Schedule changes serialize on the medication row before reminder synchronization.
        var medications = await _context.UserMedications
            .FromSqlInterpolated($"""
                SELECT *
                FROM "UserMedication"
                WHERE "UserMedicationId" = {medicationId}
                  AND "UserId" = {userId}
                  AND "IsDeleted" = FALSE
                  AND "SourceType" = {PatientReportedSourceValue}
                  AND "TreatmentJourneyId" IS NULL
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        var medication = medications.SingleOrDefault();
        if (medication is null)
        {
            return null;
        }

        await _context.Entry(medication)
            .Collection(currentMedication => currentMedication.ReminderTimes)
            .Query()
            .OrderBy(reminderTime => reminderTime.TimeOfDay)
            .ThenBy(reminderTime => reminderTime.Id)
            .LoadAsync(cancellationToken);

        return medication;
    }

    public async Task<IReadOnlyList<MedicationReminderScheduleData>>
        GetActiveSchedulesAsync(
            DateOnly earliestLocalDate,
            DateOnly latestLocalDate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        return await (
            from reminderTime in _context.UserMedicationReminderTimes.AsNoTracking()
            join medication in PatientReportedIndependentMedications(
                    _context.UserMedications.AsNoTracking())
                on reminderTime.UserMedicationId equals medication.Id
            join user in _context.Users.AsNoTracking()
                on medication.UserId equals user.Id
            where !reminderTime.IsDeleted
                  && reminderTime.IsActive
                  && medication.IsReminderEnabled
                  && medication.StartDate.HasValue
                  && medication.EndDate.HasValue
                  && medication.StartDate.Value <= latestLocalDate
                  && medication.EndDate.Value >= earliestLocalDate
                  && !user.IsDeleted
                  && user.Status == UserStatus.Confirmed
            orderby reminderTime.Id
            select new MedicationReminderScheduleData(
                reminderTime.Id,
                medication.UserId,
                medication.StartDate!.Value,
                medication.EndDate!.Value,
                reminderTime.TimeOfDay,
                user.TimeZoneId))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public void Add(UserMedication medication)
    {
        _context.UserMedications.Add(medication);
    }

    private IQueryable<UserMedication> ReadQuery()
    {
        return PatientReportedIndependentMedications(
                _context.UserMedications.AsNoTracking())
            .Include(medication => medication.ReminderTimes.Where(
                reminderTime => !reminderTime.IsDeleted))
            .AsSplitQuery();
    }

    private static IQueryable<UserMedication> PatientReportedIndependentMedications(
        IQueryable<UserMedication> medications)
    {
        return medications.Where(medication =>
            !medication.IsDeleted
            && medication.SourceType == UserMedicationSourceType.PatientReported
            && medication.TreatmentJourneyId == null);
    }

    private void EnsureActiveTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Medication schedule writes require an active database transaction.");
        }
    }
}
