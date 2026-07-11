using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class Doctor : BaseEntity
{
    public Guid? UserId { get; set; }
    
    public Guid FacilityDepartmentId { get; set; }

    public string? FullName { get; set; }

    public string? Specialty { get; set; }

    public string? AcademicTitle { get; set; }

    public string? ImageUrl { get; set; }

    public DepartmentRole DepartmentRole { get; set; } = DepartmentRole.Doctor;

    public int? YearsOfExperience { get; set; }

    public bool IsActive { get; set; }

    public FacilityDepartment FacilityDepartment { get; set; } = null!;

    public ICollection<TreatmentJourney> TreatmentJourneys { get; set; } = new List<TreatmentJourney>();
}
