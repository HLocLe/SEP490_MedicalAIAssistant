using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MedMateAI.Application.IService;
using MedMateAI.Infrastructure.Email.Brevo.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.Email.Brevo;

public sealed class BrevoSmsSender : ISmsSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Regex NonDigitRegex = new(
        @"\D",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private readonly HttpClient _httpClient;
    private readonly BrevoOptions _options;
    private readonly ILogger<BrevoSmsSender> _logger;

    public BrevoSmsSender(
        HttpClient httpClient,
        IOptions<BrevoOptions> options,
        ILogger<BrevoSmsSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string phoneNumber,
        string messageContent,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions();

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("Recipient phone number is required.", nameof(phoneNumber));
        }

        if (string.IsNullOrWhiteSpace(messageContent))
        {
            throw new ArgumentException("SMS content is required.", nameof(messageContent));
        }

        var recipientPhone = FormatPhoneNumber(phoneNumber);
        var payload = new
        {
            type = "transactional",
            sender = _options.SmsSender.Trim(),
            recipient = recipientPhone,
            content = messageContent.Trim(),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.SmsApiUrl.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("api-key", _options.ApiKey.Trim());
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "Brevo SMS API failed with status code {StatusCode}. Response: {ResponseBody}",
            (int)response.StatusCode,
            Truncate(responseBody, 500));

        return false;
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.Equals(_options.ApiKey, "YOUR_BREVO_API_KEY", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Brevo:ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.SmsApiUrl))
        {
            throw new InvalidOperationException("Brevo:SmsApiUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.SmsSender))
        {
            throw new InvalidOperationException("Brevo:SmsSender is required.");
        }
    }

    internal static string FormatPhoneNumber(string phone)
    {
        var cleaned = NonDigitRegex.Replace(phone, string.Empty);
        if (cleaned.StartsWith("0", StringComparison.Ordinal))
        {
            return "84" + cleaned[1..];
        }

        return cleaned;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..maxLength];
    }
}
