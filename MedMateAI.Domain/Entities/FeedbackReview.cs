namespace MedMateAI.Domain.Entities;

public sealed class FeedbackReview : BaseEntity
{
    public Guid UserId { get; set; }

    public Guid FacilityId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public Dictionary<string, string> ImageUrls { get; set; } = new();

    public string? Status { get; set; }

    public MedicalFacility Facility { get; set; } = null!;
}
