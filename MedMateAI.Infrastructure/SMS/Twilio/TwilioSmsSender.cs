using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using MedMateAI.Application.IService;
using MedMateAI.Infrastructure.SMS.Twilio.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.SMS.Twilio;

public sealed class TwilioSmsSender : ISmsSender
{
    private static readonly Regex NonDigitRegex = new(
        @"\D",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private readonly HttpClient _httpClient;
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(
        HttpClient httpClient,
        IOptions<TwilioOptions> options,
        ILogger<TwilioSmsSender> logger)
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
        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_options.AccountSid.Trim()}/Messages.json";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var authBytes = Encoding.ASCII.GetBytes($"{_options.AccountSid.Trim()}:{_options.AuthToken.Trim()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = recipientPhone,
            ["From"] = _options.FromPhoneNumber.Trim(),
            ["Body"] = messageContent.Trim(),
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "Twilio SMS API failed with status code {StatusCode}. Response: {ResponseBody}",
            (int)response.StatusCode,
            Truncate(responseBody, 500));

        return false;
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.AccountSid))
        {
            throw new InvalidOperationException("Twilio:AccountSid is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.AuthToken))
        {
            throw new InvalidOperationException("Twilio:AuthToken is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromPhoneNumber))
        {
            throw new InvalidOperationException("Twilio:FromPhoneNumber is required.");
        }
    }

    /// <summary>
    /// Formats phone to E.164. Vietnamese local numbers starting with 0 become +84...
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
            return "+84" + cleaned[1..];
        }

        if (cleaned.StartsWith("84", StringComparison.Ordinal))
        {
            return "+" + cleaned;
        }

        return "+" + cleaned;
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
