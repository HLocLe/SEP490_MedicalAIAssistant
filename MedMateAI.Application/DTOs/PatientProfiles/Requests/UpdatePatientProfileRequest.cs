namespace MedMateAI.Application.DTOs.PatientProfiles.Requests;

public sealed class UpdatePatientProfileRequest
{
    public string? BloodType { get; set; }

    public double? Height { get; set; }

    public double? Weight { get; set; }

    public string? AllergyNote { get; set; }

    public IReadOnlyList<PatientChronicDiseaseItemUpdateRequest>? ChronicDiseases { get; set; }
}
