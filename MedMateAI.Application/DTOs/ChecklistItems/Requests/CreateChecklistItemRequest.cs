namespace MedMateAI.Application.DTOs.ChecklistItems.Requests;

public sealed class CreateChecklistItemRequest
{
    public string Content { get; set; } = string.Empty;

    public Guid? DepartmentId { get; set; }

    public Guid? FacilityId { get; set; }

    public bool IsMandatory { get; set; }
}
