namespace MedMateAI.Application.DTOs.PatientProfiles.Requests;

public sealed class PatientChronicDiseaseItemCreateRequest
{
    public string DiseaseName { get; set; } = string.Empty;

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public string? Note { get; set; }
}
