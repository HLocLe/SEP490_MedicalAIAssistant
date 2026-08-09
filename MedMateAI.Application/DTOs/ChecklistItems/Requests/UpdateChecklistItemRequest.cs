namespace MedMateAI.Application.DTOs.ChecklistItems.Requests;

public sealed class UpdateChecklistItemRequest
{
    public string? Content { get; set; }

    /// <summary>
    /// Omit to keep current value. Send Guid.Empty to clear.
    /// </summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>
    /// Omit to keep current value. Send Guid.Empty to clear.
    /// </summary>
    public Guid? FacilityId { get; set; }

    public bool? IsMandatory { get; set; }
}
