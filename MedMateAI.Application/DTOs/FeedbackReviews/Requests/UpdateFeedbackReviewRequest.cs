namespace MedMateAI.Application.DTOs.FeedbackReviews.Requests;

public sealed class UpdateFeedbackReviewRequest
{
    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public string? ImageUrl { get; set; }
}
