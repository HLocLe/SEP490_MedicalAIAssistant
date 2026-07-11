using System.Text;
using System.Text.Json;
using MedMateAI.Application.IService;
using MedMateAI.Infrastructure.Translation.DTOs;
using MedMateAI.Infrastructure.Translation.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.Translation;

public sealed class AzureTranslationService : ITranslationService
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = null,
    };

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly AzureTranslatorOptions _options;
    private readonly ILogger<AzureTranslationService> _logger;

    public AzureTranslationService(
        HttpClient httpClient,
        IOptions<AzureTranslatorOptions> options,
        ILogger<AzureTranslationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> TranslateToEnglishAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var results = await TranslateBatchAsync(
            new[] { text.Trim() },
            _options.DefaultSourceLanguage,
            _options.DefaultTargetLanguage,
            cancellationToken);

        return results[0];
    }

    public Task<IReadOnlyList<string>> TranslateBatchToVietnameseAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts is null || texts.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        return TranslateBatchAsync(
            texts,
            _options.DefaultTargetLanguage,
            _options.DefaultSourceLanguage,
            cancellationToken);
    }

    private async Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> texts,
        string from,
        string to,
        CancellationToken cancellationToken)
    {
        var normalized = texts
            .Select(text => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim())
            .ToArray();

        if (normalized.Length == 0)
        {
            return normalized;
        }

        if (normalized.All(string.IsNullOrEmpty))
        {
            return normalized;
        }

        var requestUri = BuildTranslateUri(from, to);
        var payload = JsonSerializer.Serialize(
            normalized.Select(text => new AzureTranslateRequest { Text = text }).ToArray(),
            RequestJsonOptions);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
        httpRequest.Headers.Add("Ocp-Apim-Subscription-Key", _options.SubscriptionKey);
        httpRequest.Headers.Add("Ocp-Apim-Subscription-Region", _options.Region);
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);

            var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Azure Translator request failed with status {StatusCode}. Response: {ResponseBody}",
                    (int)httpResponse.StatusCode,
                    Truncate(responseBody, 500));

                return normalized;
            }

            var results = JsonSerializer.Deserialize<AzureTranslateResponseItem[]>(responseBody, ResponseJsonOptions);
            if (results is null || results.Length != normalized.Length)
            {
                _logger.LogWarning(
                    "Azure Translator returned an unexpected batch size. Expected {ExpectedCount}, got {ActualCount}. Using original texts.",
                    normalized.Length,
                    results?.Length ?? 0);

                return normalized;
            }

            var translated = new string[normalized.Length];

            for (var i = 0; i < normalized.Length; i++)
            {
                if (string.IsNullOrEmpty(normalized[i]))
                {
                    translated[i] = string.Empty;
                    continue;
                }

                var translatedText = results[i]
                    .Translations?
                    .FirstOrDefault()?
                    .Text;

                translated[i] = string.IsNullOrWhiteSpace(translatedText)
                    ? normalized[i]
                    : translatedText.Trim();
            }

            return translated;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Azure Translator request failed. Using original texts.");
            return normalized;
        }
    }

    private string BuildTranslateUri(string sourceLanguage, string targetLanguage)
    {
        var endpoint = _options.Endpoint.Trim();
        if (!endpoint.EndsWith('/'))
        {
            endpoint += "/";
        }

        return $"{endpoint}translate?api-version=3.0&from={Uri.EscapeDataString(sourceLanguage)}&to={Uri.EscapeDataString(targetLanguage)}";
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
