using System.Text.Json.Serialization;

namespace MedMateAI.Infrastructure.ComputerVision.DTOs;

public sealed class AzureAnalyzeRequest
{
    [JsonPropertyName("urlSource")]
    public string UrlSource { get; set; } = string.Empty;
}
