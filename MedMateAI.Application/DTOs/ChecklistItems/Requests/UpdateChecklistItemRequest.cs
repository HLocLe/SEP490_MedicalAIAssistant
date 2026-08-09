namespace MedMateAI.Application.DTOs.ChecklistItems.Requests;

public sealed class UpdateChecklistItemRequest
{
    public string? Content { get; set; }

    
    public Guid? DepartmentId { get; set; }

    public Guid? FacilityId { get; set; }

    public bool? IsMandatory { get; set; }
}
