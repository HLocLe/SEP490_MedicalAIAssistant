using System.Text.Json.Serialization;

namespace MedMateAI.Application.DTOs.FeedbackReviews.Responses;

public sealed class FeedbackReviewResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid FacilityId { get; set; }

    public string? FacilityName { get; set; }

    public string? FacilityAddress { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    [JsonPropertyName("imageUrls")]
    public Dictionary<string, string> ImageUrls { get; set; } = new();

    public string? Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
