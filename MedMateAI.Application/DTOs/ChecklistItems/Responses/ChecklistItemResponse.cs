namespace MedMateAI.Application.DTOs.ChecklistItems.Responses;

public sealed class ChecklistItemResponse
{
    public Guid Id { get; set; }

    public string Content { get; set; } = string.Empty;

    public Guid? DepartmentId { get; set; }

    public Guid? FacilityId { get; set; }

    public bool IsMandatory { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
