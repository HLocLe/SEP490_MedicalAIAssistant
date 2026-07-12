using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.MedicalFacilities.Requests;

public sealed class CreateMedicalFacilityRequest
{
    public string FacilityName { get; set; } = string.Empty;

    public string? Address { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public string? ImageUrl { get; set; }

    public string? OpeningHours { get; set; }

    public MedicalFacilityType FacilityType { get; set; } = MedicalFacilityType.Hospital;

    public bool IsActive { get; set; } = true;

    public IReadOnlyList<Guid> DepartmentIds { get; set; } = Array.Empty<Guid>();
}
