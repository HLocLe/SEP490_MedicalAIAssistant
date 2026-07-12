namespace MedMateAI.Application.DTOs.FeedbackReviews.Requests;

public sealed class CreateFeedbackReviewRequest
{
    public Guid FacilityId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public string? ImageUrl { get; set; }
}
