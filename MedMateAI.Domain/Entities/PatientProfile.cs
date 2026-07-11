namespace MedMateAI.Domain.Entities;

public sealed class PatientProfile : BaseEntity
{
    public Guid UserId { get; set; }

    public string? BloodType { get; set; }

    public double? Height { get; set; }

    public double? Weight { get; set; }

    public string? AllergyNote { get; set; }

    public ICollection<PatientChronicDisease> ChronicDiseases { get; set; } =
        new List<PatientChronicDisease>();
}
