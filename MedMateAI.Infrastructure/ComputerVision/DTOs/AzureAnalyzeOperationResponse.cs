using System.Text.Json.Serialization;

namespace MedMateAI.Infrastructure.ComputerVision.DTOs;

public sealed class AzureAnalyzeOperationResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("analyzeResult")]
    public AzureAnalyzeResult? AnalyzeResult { get; set; }

    [JsonPropertyName("error")]
    public AzureAnalyzeError? Error { get; set; }
}

public sealed class AzureAnalyzeResult
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public sealed class AzureAnalyzeError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
