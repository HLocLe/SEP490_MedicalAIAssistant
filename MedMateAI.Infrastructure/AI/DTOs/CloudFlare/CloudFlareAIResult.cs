using System.Text.Json.Serialization;

namespace MedMateAI.Infrastructure.AI.DTOs.CloudFlare;

internal sealed class CloudFlareAIResult
{
    [JsonPropertyName("response")]
    public string? Response { get; set; }
}
