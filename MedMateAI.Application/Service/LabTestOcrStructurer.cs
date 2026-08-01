using System.Globalization;
using System.Text.Json;
using MedMateAI.Application.DTOs.LabTests.Ocr;
using MedMateAI.Application.DTOs.WebChatbot.Requests;
using MedMateAI.Application.IService;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class LabTestOcrStructurer : ILabTestOcrStructurer
{
    private const string OcrStructuringTaskType = "LabTestOcrStructuring";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAIConfigService _aiConfigService;
    private readonly IAIChatProvider _chatProvider;
    private readonly ILogger<LabTestOcrStructurer> _logger;

    public LabTestOcrStructurer(
        IAIConfigService aiConfigService,
        IAIChatProvider chatProvider,
        ILogger<LabTestOcrStructurer> logger)
    {
        _aiConfigService = aiConfigService;
        _chatProvider = chatProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ParsedOcrRow>> StructureAsync(
        string rawOcrText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawOcrText))
        {
            return Array.Empty<ParsedOcrRow>();
        }

        var aiConfig = await _aiConfigService.GetActiveAIConfigByTaskTypeAsync(
            OcrStructuringTaskType,
            cancellationToken);

        if (aiConfig is null || string.IsNullOrWhiteSpace(aiConfig.SystemPrompt))
        {
            throw new InvalidOperationException(
                $"No active AI config with a system prompt found for task type '{OcrStructuringTaskType}'.");
        }

        var result = await _chatProvider.GenerateAsync(
            new AIProviderChatRequest
            {
                SystemPrompt = aiConfig.SystemPrompt,
                UserMessage = rawOcrText,
                Model = aiConfig.Model ?? string.Empty,
                Temperature = aiConfig.Temperature,
                MaxTokens = aiConfig.MaxTokens,
            },
            cancellationToken);

        return ParseRows(result.Content);
    }

    private IReadOnlyList<ParsedOcrRow> ParseRows(string content)
    {
        var json = ExtractJsonObject(content);

        StructuredLabOcrDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<StructuredLabOcrDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse AI OCR structuring response as JSON. Content: {Content}",
                Truncate(content, 500));

            throw new InvalidOperationException(
                "AI OCR structuring response is not a valid JSON object.",
                ex);
        }

        var structuredRows = document?.DanhSachXetNghiem;
        if (structuredRows is null || structuredRows.Count == 0)
        {
            return Array.Empty<ParsedOcrRow>();
        }

        var rows = new List<ParsedOcrRow>();

        foreach (var row in structuredRows)
        {
            if (string.IsNullOrWhiteSpace(row.TenXetNghiem))
            {
                continue;
            }

            rows.Add(new ParsedOcrRow(
                row.TenXetNghiem.Trim(),
                NormalizeText(row.TriSoBinhThuong),
                ParseValue(row.KetQua)));
        }

        return rows;
    }

    // LLMs often wrap JSON output in markdown code fences or add surrounding prose.
    private static string ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException(
                $"AI OCR structuring response does not contain a JSON object. Content: {Truncate(trimmed, 500)}");
        }

        return trimmed[start..(end + 1)];
    }

    // "ket_qua" may use a Vietnamese decimal comma, e.g. "5,1".
    private static double? ParseValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var normalized = rawValue.Trim().Replace(',', '.');

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
