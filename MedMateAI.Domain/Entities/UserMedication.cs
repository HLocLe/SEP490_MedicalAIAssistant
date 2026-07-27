using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class UserMedication : BaseEntity
{
    public Guid UserId { get; set; }

    public string MedicineName { get; set; } = string.Empty;

    public Guid? TreatmentJourneyId { get; set; }

    public string? DosageInstruction { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Status { get; set; }

    public UserMedicationSourceType SourceType { get; set; } = UserMedicationSourceType.PatientReported;

    public bool IsReminderEnabled { get; set; }

    public TreatmentJourney? TreatmentJourney { get; set; }

    public ICollection<UserMedicationReminderTime> ReminderTimes { get; set; } = new List<UserMedicationReminderTime>();
}
