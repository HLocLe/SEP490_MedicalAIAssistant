namespace MedMateAI.Domain.Entities;

public sealed class ChecklistItem : BaseEntity
{
    public string Content { get; set; } = string.Empty;

    public Guid? DepartmentId { get; set; }

    public Guid? FacilityId { get; set; }

    public bool IsMandatory { get; set; }

    public MedicalDepartment? Department { get; set; }

    public MedicalFacility? Facility { get; set; }
}
