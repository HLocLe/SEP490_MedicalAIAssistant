namespace MedMateAI.Application.DTOs.PatientProfiles.Requests;


public sealed class CreatePatientProfileRequest
{
    public Guid UserId { get; set; }

    public string? BloodType { get; set; }

    public double? Height { get; set; }

    public double? Weight { get; set; }

    public string? AllergyNote { get; set; }

    public IReadOnlyList<PatientChronicDiseaseItemCreateRequest>? ChronicDiseases { get; set; }
}
