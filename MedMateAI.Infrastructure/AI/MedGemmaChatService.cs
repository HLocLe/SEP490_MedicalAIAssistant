using System.Net.Http.Headers;
using System.Text.Json;
using MedMateAI.Application.DTOs.SymptomAnalysis.Responses.MedGemma;
using MedMateAI.Application.IService;
using MedMateAI.Infrastructure.AI.DTOs.MedGemma;
using MedMateAI.Infrastructure.AI.Helpers;
using MedMateAI.Infrastructure.AI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.AI;

public sealed class MedGemmaChatService : IMedGemmaChatService
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly MedGemmaOptions _options;
    private readonly ILogger<MedGemmaChatService> _logger;

    public MedGemmaChatService(
        HttpClient httpClient,
        IOptions<MedGemmaOptions> options,
        ILogger<MedGemmaChatService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MedGemmaChatResult> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("MedGemma:BaseUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException("MedGemma:Model is not configured.");
        }

        var apiKey = string.IsNullOrWhiteSpace(_options.ApiKey) ? "EMPTY" : _options.ApiKey.Trim();
        var baseUrl = _options.BaseUrl.Trim().TrimEnd('/');
        var model = _options.Model.Trim();

        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "user", content = prompt.Trim() },
            },
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        AiHttpClientHelper.SetJsonContent(httpRequest, payload);

        var responseBody = await AiHttpClientHelper.SendJsonAsync(
            _httpClient,
            httpRequest,
            _logger,
            "MedGemma",
            cancellationToken);

        MedGemmaChatCompletionResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<MedGemmaChatCompletionResponse>(responseBody, ResponseJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse MedGemma response.", ex);
        }

        var content = response?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("MedGemma response does not contain message content.");
        }

        return new MedGemmaChatResult
        {
            Content = content.Trim(),
            Model = response?.Model ?? model,
        };
    }
}
