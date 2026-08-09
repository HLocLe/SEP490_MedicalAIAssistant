namespace MedMateAI.Application.DTOs.ChecklistItems.Requests;

public sealed class BulkCreateChecklistItemsRequest
{
    public List<CreateChecklistItemRequest> Items { get; set; } = new();
}
