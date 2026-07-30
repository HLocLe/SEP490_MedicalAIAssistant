using MedMateAI.Application.DTOs.UserMedications;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.UserMedications;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class UserMedicationService : IUserMedicationService
{
    private const int MaximumMedicineNameLength = 256;
    private const int MaximumDosageInstructionLength = 1000;
    private const int MaximumReminderTimes = 12;

    private readonly IUnitOfWork _unitOfWork;

    public UserMedicationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserMedicationOperationResult<IReadOnlyList<UserMedicationResponse>>>
        GetMineAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        var medications = await _unitOfWork.UserMedications.GetByUserIdAsync(
            userId,
            cancellationToken);
        var responses = medications
            .Select(Map)
            .ToList();

        return UserMedicationOperationResult<IReadOnlyList<UserMedicationResponse>>
            .Ok(responses);
    }

    public async Task<UserMedicationOperationResult<UserMedicationResponse>> GetByIdAsync(
        Guid userId,
        Guid medicationId,
        CancellationToken cancellationToken = default)
    {
        var medication = await _unitOfWork.UserMedications.GetByIdAsync(
            userId,
            medicationId,
            cancellationToken);

        if (medication is null)
        {
            return NotFound();
        }

        return UserMedicationOperationResult<UserMedicationResponse>.Ok(
            Map(medication));
    }

    public async Task<UserMedicationOperationResult<UserMedicationResponse>> CreateAsync(
        Guid userId,
        CreateUserMedicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(
            request.MedicineName,
            request.DosageInstruction,
            request.StartDate,
            request.EndDate,
            request.IsReminderEnabled,
            request.ReminderTimes);
        if (!validation.Success)
        {
            return UserMedicationOperationResult<UserMedicationResponse>.Fail(
                UserMedicationErrorCode.InvalidRequest,
                validation.Error);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var utcNow = DateTime.UtcNow;
            var values = validation.Values!;
            var medication = new UserMedication
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MedicineName = values.MedicineName,
                TreatmentJourneyId = null,
                DosageInstruction = values.DosageInstruction,
                StartDate = values.StartDate,
                EndDate = values.EndDate,
                Status = null,
                SourceType = UserMedicationSourceType.PatientReported,
                IsReminderEnabled = values.IsReminderEnabled,
                CreatedAt = utcNow
            };

            SynchronizeReminderTimes(medication, values.ReminderTimes, utcNow);
            _unitOfWork.UserMedications.Add(medication);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return UserMedicationOperationResult<UserMedicationResponse>.Ok(
                Map(medication));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public async Task<UserMedicationOperationResult<UserMedicationResponse>> UpdateAsync(
        Guid userId,
        Guid medicationId,
        UpdateUserMedicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(
            request.MedicineName,
            request.DosageInstruction,
            request.StartDate,
            request.EndDate,
            request.IsReminderEnabled,
            request.ReminderTimes);
        if (!validation.Success)
        {
            return UserMedicationOperationResult<UserMedicationResponse>.Fail(
                UserMedicationErrorCode.InvalidRequest,
                validation.Error);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var medication = await _unitOfWork.UserMedications.GetByIdForUpdateAsync(
                userId,
                medicationId,
                cancellationToken);
            if (medication is null)
            {
                return await RollbackNotFoundAsync<UserMedicationResponse>();
            }

            var utcNow = DateTime.UtcNow;
            var values = validation.Values!;
            medication.MedicineName = values.MedicineName;
            medication.DosageInstruction = values.DosageInstruction;
            medication.StartDate = values.StartDate;
            medication.EndDate = values.EndDate;
            medication.IsReminderEnabled = values.IsReminderEnabled;
            medication.UpdatedAt = utcNow;

            SynchronizeReminderTimes(medication, values.ReminderTimes, utcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return UserMedicationOperationResult<UserMedicationResponse>.Ok(
                Map(medication));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public async Task<UserMedicationOperationResult<bool>> DeleteAsync(
        Guid userId,
        Guid medicationId,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var medication = await _unitOfWork.UserMedications.GetByIdForUpdateAsync(
                userId,
                medicationId,
                cancellationToken);
            if (medication is null)
            {
                return await RollbackNotFoundAsync<bool>();
            }

            var utcNow = DateTime.UtcNow;
            medication.IsReminderEnabled = false;
            medication.IsDeleted = true;
            medication.DeletedAt = utcNow;
            medication.UpdatedAt = utcNow;
            DeactivateReminderTimes(medication, utcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return UserMedicationOperationResult<bool>.Ok(true);
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public async Task<UserMedicationOperationResult<UserMedicationResponse>>
        ReplaceReminderTimesAsync(
            Guid userId,
            Guid medicationId,
            ReplaceMedicationReminderTimesRequest request,
            CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var medication = await _unitOfWork.UserMedications.GetByIdForUpdateAsync(
                userId,
                medicationId,
                cancellationToken);
            if (medication is null)
            {
                return await RollbackNotFoundAsync<UserMedicationResponse>();
            }

            var validation = ValidateSchedule(
                medication.StartDate,
                medication.EndDate,
                request.IsReminderEnabled,
                request.ReminderTimes);
            if (!validation.Success)
            {
                await RollbackAsync();
                return UserMedicationOperationResult<UserMedicationResponse>.Fail(
                    UserMedicationErrorCode.InvalidRequest,
                    validation.Error);
            }

            var utcNow = DateTime.UtcNow;
            medication.IsReminderEnabled = request.IsReminderEnabled;
            medication.UpdatedAt = utcNow;
            SynchronizeReminderTimes(
                medication,
                validation.ReminderTimes!,
                utcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return UserMedicationOperationResult<UserMedicationResponse>.Ok(
                Map(medication));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    private static MedicationValidation Validate(
        string medicineName,
        string? dosageInstruction,
        DateOnly? startDate,
        DateOnly? endDate,
        bool isReminderEnabled,
        IReadOnlyList<TimeOnly>? reminderTimes)
    {
        var normalizedMedicineName = medicineName?.Trim() ?? string.Empty;
        if (normalizedMedicineName.Length is < 1 or > MaximumMedicineNameLength)
        {
            return MedicationValidation.Fail(
                "MedicineName is required and must not exceed 256 characters.");
        }

        var normalizedDosageInstruction = NormalizeOptional(dosageInstruction);
        if (normalizedDosageInstruction?.Length > MaximumDosageInstructionLength)
        {
            return MedicationValidation.Fail(
                "DosageInstruction must not exceed 1000 characters.");
        }

        var scheduleValidation = ValidateSchedule(
            startDate,
            endDate,
            isReminderEnabled,
            reminderTimes);
        if (!scheduleValidation.Success)
        {
            return MedicationValidation.Fail(scheduleValidation.Error!);
        }

        return MedicationValidation.Ok(new MedicationValues(
            normalizedMedicineName,
            normalizedDosageInstruction,
            startDate,
            endDate,
            isReminderEnabled,
            scheduleValidation.ReminderTimes!));
    }

    private static ScheduleValidation ValidateSchedule(
        DateOnly? startDate,
        DateOnly? endDate,
        bool isReminderEnabled,
        IReadOnlyList<TimeOnly>? reminderTimes)
    {
        if (startDate.HasValue
            && endDate.HasValue
            && endDate.Value < startDate.Value)
        {
            return ScheduleValidation.Fail(
                "EndDate must be greater than or equal to StartDate.");
        }

        var times = reminderTimes ?? Array.Empty<TimeOnly>();
        if (times.Count > MaximumReminderTimes)
        {
            return ScheduleValidation.Fail(
                "A medication can have at most 12 reminder times.");
        }

        if (times.Any(time => time.Ticks % TimeSpan.TicksPerMinute != 0))
        {
            return ScheduleValidation.Fail(
                "Reminder times must use minute precision.");
        }

        var distinctTimes = times
            .Distinct()
            .OrderBy(time => time)
            .ToList();
        if (distinctTimes.Count != times.Count)
        {
            return ScheduleValidation.Fail(
                "Reminder times must not contain duplicates.");
        }

        if (isReminderEnabled
            && (!startDate.HasValue
                || !endDate.HasValue
                || distinctTimes.Count == 0))
        {
            return ScheduleValidation.Fail(
                "Enabled reminders require StartDate, EndDate, and at least one reminder time.");
        }

        return ScheduleValidation.Ok(distinctTimes);
    }

    private static void SynchronizeReminderTimes(
        UserMedication medication,
        IReadOnlyList<TimeOnly> requestedTimes,
        DateTime utcNow)
    {
        var requestedSet = requestedTimes.ToHashSet();
        var currentActive = medication.ReminderTimes
            .Where(reminderTime =>
                !reminderTime.IsDeleted
                && reminderTime.IsActive)
            .ToList();

        foreach (var reminderTime in currentActive)
        {
            if (requestedSet.Contains(reminderTime.TimeOfDay))
            {
                continue;
            }

            reminderTime.IsActive = false;
            reminderTime.IsDeleted = true;
            reminderTime.DeletedAt = utcNow;
            reminderTime.UpdatedAt = utcNow;
        }

        var retainedTimes = currentActive
            .Where(reminderTime =>
                !reminderTime.IsDeleted
                && requestedSet.Contains(reminderTime.TimeOfDay))
            .Select(reminderTime => reminderTime.TimeOfDay)
            .ToHashSet();

        foreach (var requestedTime in requestedTimes)
        {
            if (retainedTimes.Contains(requestedTime))
            {
                continue;
            }

            medication.ReminderTimes.Add(new UserMedicationReminderTime
            {
                Id = Guid.NewGuid(),
                UserMedicationId = medication.Id,
                TimeOfDay = requestedTime,
                IsActive = true,
                CreatedAt = utcNow
            });
        }
    }

    private static void DeactivateReminderTimes(
        UserMedication medication,
        DateTime utcNow)
    {
        foreach (var reminderTime in medication.ReminderTimes.Where(
                     reminderTime => !reminderTime.IsDeleted))
        {
            reminderTime.IsActive = false;
            reminderTime.IsDeleted = true;
            reminderTime.DeletedAt = utcNow;
            reminderTime.UpdatedAt = utcNow;
        }
    }

    private static UserMedicationResponse Map(UserMedication medication)
    {
        return new UserMedicationResponse
        {
            Id = medication.Id,
            MedicineName = medication.MedicineName,
            DosageInstruction = medication.DosageInstruction,
            StartDate = medication.StartDate,
            EndDate = medication.EndDate,
            Status = medication.Status,
            SourceType = medication.SourceType,
            IsReminderEnabled = medication.IsReminderEnabled,
            ReminderTimes = medication.ReminderTimes
                .Where(reminderTime => !reminderTime.IsDeleted)
                .OrderBy(reminderTime => reminderTime.TimeOfDay)
                .ThenBy(reminderTime => reminderTime.Id)
                .Select(reminderTime => new UserMedicationReminderTimeResponse
                {
                    Id = reminderTime.Id,
                    TimeOfDay = reminderTime.TimeOfDay,
                    IsActive = reminderTime.IsActive
                })
                .ToList(),
            CreatedAt = medication.CreatedAt,
            UpdatedAt = medication.UpdatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalizedValue = value?.Trim();
        return string.IsNullOrEmpty(normalizedValue) ? null : normalizedValue;
    }

    private static UserMedicationOperationResult<UserMedicationResponse> NotFound()
    {
        return UserMedicationOperationResult<UserMedicationResponse>.Fail(
            UserMedicationErrorCode.NotFound);
    }

    private async Task<UserMedicationOperationResult<T>> RollbackNotFoundAsync<T>()
    {
        await RollbackAsync();
        return UserMedicationOperationResult<T>.Fail(
            UserMedicationErrorCode.NotFound);
    }

    private Task RollbackAsync()
    {
        return _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
    }

    private sealed record MedicationValues(
        string MedicineName,
        string? DosageInstruction,
        DateOnly? StartDate,
        DateOnly? EndDate,
        bool IsReminderEnabled,
        IReadOnlyList<TimeOnly> ReminderTimes);

    private sealed record MedicationValidation(
        bool Success,
        MedicationValues? Values,
        string? Error)
    {
        public static MedicationValidation Ok(MedicationValues values)
        {
            return new MedicationValidation(true, values, null);
        }

        public static MedicationValidation Fail(string error)
        {
            return new MedicationValidation(false, null, error);
        }
    }

    private sealed record ScheduleValidation(
        bool Success,
        IReadOnlyList<TimeOnly>? ReminderTimes,
        string? Error)
    {
        public static ScheduleValidation Ok(IReadOnlyList<TimeOnly> reminderTimes)
        {
            return new ScheduleValidation(true, reminderTimes, null);
        }

        public static ScheduleValidation Fail(string error)
        {
            return new ScheduleValidation(false, null, error);
        }
    }
}
