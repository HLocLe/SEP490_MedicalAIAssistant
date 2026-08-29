namespace MedMateAI.Domain.Entities;

public sealed class DiseasePriorProbability : BaseEntity
{
    public string Icd10Code { get; set; } = string.Empty;

    public string? DiseaseName { get; set; }

    public double PA { get; set; }

    public bool IsActive { get; set; } = true;
}
