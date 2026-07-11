namespace MedMateAI.Application.DTOs.PatientProfiles.Responses;

public sealed class PatientChronicDiseaseResponse
{
    public Guid Id { get; set; }

    public string DiseaseName { get; set; } = string.Empty;

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
