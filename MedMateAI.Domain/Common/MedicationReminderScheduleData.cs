namespace MedMateAI.Domain.Common;

public sealed record MedicationReminderScheduleData(
    Guid ReminderTimeId,
    Guid UserId,
    DateOnly StartDate,
    DateOnly EndDate,
    TimeOnly TimeOfDay,
    string? TimeZoneId);
