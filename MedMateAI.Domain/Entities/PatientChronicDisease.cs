namespace MedMateAI.Domain.Entities;

public sealed class PatientChronicDisease : BaseEntity
{
    public Guid PatientProfileId { get; set; }

    public string DiseaseName { get; set; } = string.Empty;

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public string? Note { get; set; }

    public PatientProfile PatientProfile { get; set; } = null!;
}
