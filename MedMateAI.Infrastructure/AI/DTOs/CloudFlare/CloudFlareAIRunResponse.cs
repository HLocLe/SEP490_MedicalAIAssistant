using System.Text.Json.Serialization;

namespace MedMateAI.Infrastructure.AI.DTOs.CloudFlare;

internal sealed class CloudFlareAIRunResponse
{
    [JsonPropertyName("result")]
    public CloudFlareAIResult? Result { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
