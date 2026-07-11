namespace MedMateAI.Application.DTOs.PatientProfiles.Responses;

public sealed class PatientProfileResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? BloodType { get; set; }

    public double? Height { get; set; }

    public double? Weight { get; set; }

    public string? AllergyNote { get; set; }

    public IReadOnlyList<PatientChronicDiseaseResponse> ChronicDiseases { get; set; } =
        Array.Empty<PatientChronicDiseaseResponse>();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public bool IsProfileCompleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
