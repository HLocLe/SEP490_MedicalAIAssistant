using MedMateAI.Application.DTOs.CloudFlareAI.Responses;

namespace MedMateAI.Application.IService;

public interface ICloudFlareAIChatService
{
    Task<CloudFlareAIChatResult> GenerateAsync(
        string? systemPrompt,
        string userPrompt,
        int? maxTokens = null,
        decimal? temperature = null,
        CancellationToken cancellationToken = default);
}
