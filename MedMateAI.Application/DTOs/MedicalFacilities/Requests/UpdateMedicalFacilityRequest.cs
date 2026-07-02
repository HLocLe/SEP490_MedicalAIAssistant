namespace MedMateAI.Application.DTOs.MedicalFacilities.Requests;

public sealed class UpdateMedicalFacilityRequest
{
    public string? FacilityName { get; set; }

    public string? Address { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public string? ImageUrl { get; set; }

    public string? OpeningHours { get; set; }

    public string? FacilityType { get; set; }

    public bool? IsActive { get; set; }

    public IReadOnlyList<Guid>? DepartmentIds { get; set; }
}
