namespace MedMateAI.Application.DTOs.CloudFlareAI.Responses;

public sealed class CloudFlareAIChatResult
{
    public string Content { get; set; } = string.Empty;

    public string? Model { get; set; }
}
