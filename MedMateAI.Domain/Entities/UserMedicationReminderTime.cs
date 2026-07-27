namespace MedMateAI.Domain.Entities;

public sealed class UserMedicationReminderTime : BaseEntity
{
    public Guid UserMedicationId { get; set; }
    public TimeOnly TimeOfDay { get; set; }
    public bool IsActive { get; set; } = true;
    public UserMedication UserMedication { get; set; } = null!;
}
