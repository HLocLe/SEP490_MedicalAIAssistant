using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MedMateAI.Application.DTOs.CloudFlareAI.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Infrastructure.AI.DTOs.CloudFlare;
using MedMateAI.Infrastructure.AI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.AI;

public sealed class CloudFlareAIChatService : ICloudFlareAIChatService
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly CloudFlareAIOptions _options;
    private readonly ILogger<CloudFlareAIChatService> _logger;

    public CloudFlareAIChatService(
        HttpClient httpClient,
        IOptions<CloudFlareAIOptions> options,
        ILogger<CloudFlareAIChatService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CloudFlareAIChatResult> GenerateAsync(
        string? systemPrompt,
        string userPrompt,
        int? maxTokens = null,
        decimal? temperature = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ArgumentException("User prompt is required.");
        }

        if (maxTokens is <= 0)
        {
            maxTokens = null;
        }

        ValidateOptions();

        var model = _options.Model.Trim();
        var payload = new
        {
            model,
            input = BuildInput(systemPrompt, userPrompt, temperature, maxTokens),
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint.Trim());
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken.Trim());
        httpRequest.Headers.TryAddWithoutValidation("cf-aig-gateway-id", _options.GatewayId.Trim());
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "CloudFlare AI request failed with status code {StatusCode}. Response: {ResponseBody}",
                (int)httpResponse.StatusCode,
                Truncate(responseBody, 500));

            throw new InvalidOperationException(
                $"CloudFlare AI request failed with status code {(int)httpResponse.StatusCode}. Response: {Truncate(responseBody, 500)}");
        }

        CloudFlareAIRunResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<CloudFlareAIRunResponse>(responseBody, ResponseJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse CloudFlare AI response.", ex);
        }

        var content = response?.Result?.Response;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("CloudFlare AI response does not contain content.");
        }

        return new CloudFlareAIChatResult
        {
            Content = content.Trim(),
            Model = model,
        };
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException("CloudFlareAI:Endpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException("CloudFlareAI:Model is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            throw new InvalidOperationException("CloudFlareAI:ApiToken is not configured.");
        }
    }

    private static object BuildInput(
        string? systemPrompt,
        string userPrompt,
        decimal? temperature,
        int? maxTokens)
    {
        if (maxTokens.HasValue)
        {
            return new
            {
                messages = BuildMessages(systemPrompt, userPrompt),
                response_format = new { type = "json_object" },
                temperature,
                max_tokens = maxTokens.Value,
            };
        }

        return new
        {
            messages = BuildMessages(systemPrompt, userPrompt),
            response_format = new { type = "json_object" },
            temperature,
        };
    }

    private static object[] BuildMessages(string? systemPrompt, string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            return [new { role = "user", content = userPrompt.Trim() }];
        }

        return
        [
            new { role = "system", content = systemPrompt.Trim() },
            new { role = "user", content = userPrompt.Trim() },
        ];
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
