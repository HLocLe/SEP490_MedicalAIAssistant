using System.Text.Json.Serialization;

namespace MedMateAI.Application.DTOs.FeedbackReviews.Requests;

public sealed class UpdateFeedbackReviewRequest
{
    public int? Rating { get; set; }

    public string? Comment { get; set; }

    [JsonPropertyName("imageUrls")]
    public Dictionary<string, string?>? ImageUrls { get; set; }
}
