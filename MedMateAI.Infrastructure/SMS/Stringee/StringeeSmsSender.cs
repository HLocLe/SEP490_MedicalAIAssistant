using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MedMateAI.Application.IService;
using MedMateAI.Infrastructure.SMS.Stringee.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MedMateAI.Infrastructure.SMS.Stringee;

public sealed class StringeeSmsSender : ISmsSender
{
    private static readonly Regex NonDigitRegex = new(
        @"\D",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private readonly HttpClient _httpClient;
    private readonly StringeeOptions _options;
    private readonly ILogger<StringeeSmsSender> _logger;

    public StringeeSmsSender(
        HttpClient httpClient,
        IOptions<StringeeOptions> options,
        ILogger<StringeeSmsSender> logger)
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
        var jwtToken = GenerateJwtToken();
        const string url = "https://api.stringee.com/v1/sms";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("X-STRINGEE-AUTH", jwtToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new
        {
            sms = new[]
            {
                new
                {
                    from = _options.FromSender.Trim(),
                    to = recipientPhone,
                    text = messageContent.Trim(),
                },
            },
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode && IsStringeeSuccess(responseBody))
        {
            _logger.LogInformation("Stringee SMS sent successfully to {PhoneNumber}", phoneNumber);
            return true;
        }

        _logger.LogWarning(
            "Stringee SMS API failed with status code {StatusCode}. Response: {ResponseBody}",
            (int)response.StatusCode,
            Truncate(responseBody, 500));

        return false;
    }

    private string GenerateJwtToken()
    {
        var now = DateTimeOffset.UtcNow;
        var apiKeySid = _options.ApiKeySid.Trim();
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiKeySecret.Trim()));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var header = new JwtHeader(credentials)
        {
            ["cty"] = "stringee-api;v=1",
        };

        var payload = new JwtPayload
        {
            { "jti", $"{apiKeySid}-{now.ToUnixTimeSeconds()}" },
            { "iss", apiKeySid },
            { "exp", now.AddHours(1).ToUnixTimeSeconds() },
            { "rest_api", true },
        };

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKeySid))
        {
            throw new InvalidOperationException("Stringee:ApiKeySid is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKeySecret))
        {
            throw new InvalidOperationException("Stringee:ApiKeySecret is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromSender))
        {
            throw new InvalidOperationException("Stringee:FromSender is required.");
        }
    }

    /// <summary>
    /// Formats phone to Stringee sample format: 84xxxxxxxxx (no plus).
    /// Vietnamese local numbers starting with 0 become 84...
    /// </summary>
    internal static string FormatPhoneNumber(string phone)
    {
        var cleaned = NonDigitRegex.Replace(phone, string.Empty);
        if (string.IsNullOrEmpty(cleaned))
        {
            return phone.Trim();
        }

        if (cleaned.StartsWith("0", StringComparison.Ordinal))
        {
            return "84" + cleaned[1..];
        }

        if (cleaned.StartsWith("84", StringComparison.Ordinal))
        {
            return cleaned;
        }

        return cleaned;
    }

    private static bool IsStringeeSuccess(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("r", out var resultCode))
            {
                return resultCode.ValueKind == JsonValueKind.Number && resultCode.GetInt32() == 0;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return true;
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
