using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.Doctors.Requests;

public sealed class UpdateDoctorRequest
{
    public Guid? FacilityDepartmentId { get; set; }

    public string? FullName { get; set; }

    public string? Specialty { get; set; }

    public string? AcademicTitle { get; set; }

    public string? ImageUrl { get; set; }

    public DepartmentRole? DepartmentRole { get; set; }

    public int? YearsOfExperience { get; set; }

    public bool? IsActive { get; set; }
}
