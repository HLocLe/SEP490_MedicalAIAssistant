using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.Doctors.Responses;

public sealed class DoctorResponse
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public Guid FacilityDepartmentId { get; set; }

    public Guid FacilityId { get; set; }

    public string? FacilityName { get; set; }

    public Guid DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Specialty { get; set; }

    public string? AcademicTitle { get; set; }

    public string? ImageUrl { get; set; }

    public DepartmentRole DepartmentRole { get; set; }

    public string DepartmentRoleName { get; set; } = string.Empty;

    public int? YearsOfExperience { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
