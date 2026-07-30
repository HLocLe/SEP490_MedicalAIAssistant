using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.UserMedications;

public sealed class CreateUserMedicationRequest
{
    public string MedicineName { get; set; } = string.Empty;
    public string? DosageInstruction { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsReminderEnabled { get; set; }
    public IReadOnlyList<TimeOnly> ReminderTimes { get; set; } = Array.Empty<TimeOnly>();
}

public sealed class UpdateUserMedicationRequest
{
    public string MedicineName { get; set; } = string.Empty;
    public string? DosageInstruction { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsReminderEnabled { get; set; }
    public IReadOnlyList<TimeOnly> ReminderTimes { get; set; } = Array.Empty<TimeOnly>();
}

public sealed class ReplaceMedicationReminderTimesRequest
{
    public bool IsReminderEnabled { get; set; }
    public IReadOnlyList<TimeOnly> ReminderTimes { get; set; } = Array.Empty<TimeOnly>();
}

public sealed class UserMedicationReminderTimeResponse
{
    public Guid Id { get; set; }
    public TimeOnly TimeOfDay { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UserMedicationResponse
{
    public Guid Id { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? DosageInstruction { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Status { get; set; }
    public UserMedicationSourceType SourceType { get; set; }
    public bool IsReminderEnabled { get; set; }
    public IReadOnlyList<UserMedicationReminderTimeResponse> ReminderTimes { get; set; } =
        Array.Empty<UserMedicationReminderTimeResponse>();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
