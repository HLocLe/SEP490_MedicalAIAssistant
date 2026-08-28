using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Notifications;
using MedMateAI.Infrastructure.Push.Expo.Options;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.Push.Expo;

public sealed class ExpoPushGateway : IPushNotificationGateway
{
    private const string ProviderUnavailable = "PUSH_PROVIDER_UNAVAILABLE";
    private const string ProviderRejected = "PUSH_PROVIDER_REJECTED";
    private const string InvalidProviderResponse = "PUSH_PROVIDER_INVALID_RESPONSE";
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ExpoPushOptions _options;

    public ExpoPushGateway(
        HttpClient httpClient,
        IOptions<ExpoPushOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<PushSendResult> SendAsync(
        PushNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            _options.SendEndpoint,
            new ExpoSendRequest(
                message.ExpoPushToken,
                message.Title,
                message.Body,
                "high",
                "medimate-notifications",
                message.Data,
                message.TimeToLiveSeconds));

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return IsRetryable(response.StatusCode)
                    ? new PushSendResult(
                        PushSendOutcome.RetryableFailure,
                        ErrorCode: ProviderUnavailable)
                    : new PushSendResult(
                        PushSendOutcome.PermanentFailure,
                        ErrorCode: ProviderRejected);
            }

            await using var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            var ticket = GetFirstTicket(document.RootElement);
            if (!ticket.HasValue
                || !ticket.Value.TryGetProperty("status", out var status))
            {
                return new PushSendResult(
                    PushSendOutcome.RetryableFailure,
                    ErrorCode: InvalidProviderResponse);
            }

            if (string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase)
                && ticket.Value.TryGetProperty("id", out var id)
                && !string.IsNullOrWhiteSpace(id.GetString()))
            {
                return new PushSendResult(
                    PushSendOutcome.Accepted,
                    id.GetString());
            }

            var providerError = GetProviderError(ticket.Value);
            return MapSendError(providerError);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PushSendResult(
                PushSendOutcome.RetryableFailure,
                ErrorCode: ProviderUnavailable);
        }
        catch (HttpRequestException)
        {
            return new PushSendResult(
                PushSendOutcome.RetryableFailure,
                ErrorCode: ProviderUnavailable);
        }
        catch (JsonException)
        {
            return new PushSendResult(
                PushSendOutcome.RetryableFailure,
                ErrorCode: InvalidProviderResponse);
        }
    }

    public async Task<PushReceiptBatchResult> GetReceiptsAsync(
        IReadOnlyCollection<string> providerMessageIds,
        CancellationToken cancellationToken = default)
    {
        if (providerMessageIds.Count is < 1 or > 1000)
        {
            return FailedReceiptBatch(false, ProviderRejected);
        }

        using var request = CreateRequest(
            HttpMethod.Post,
            _options.ReceiptEndpoint,
            new ExpoReceiptRequest(providerMessageIds));

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return FailedReceiptBatch(
                    IsRetryable(response.StatusCode),
                    IsRetryable(response.StatusCode)
                        ? ProviderUnavailable
                        : ProviderRejected);
            }

            await using var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object)
            {
                return FailedReceiptBatch(true, InvalidProviderResponse);
            }

            var receipts = new Dictionary<string, PushReceiptResult>(
                StringComparer.Ordinal);
            foreach (var receipt in data.EnumerateObject())
            {
                receipts[receipt.Name] = MapReceipt(receipt.Value);
            }

            return new PushReceiptBatchResult(true, false, receipts);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FailedReceiptBatch(true, ProviderUnavailable);
        }
        catch (HttpRequestException)
        {
            return FailedReceiptBatch(true, ProviderUnavailable);
        }
        catch (JsonException)
        {
            return FailedReceiptBatch(true, InvalidProviderResponse);
        }
    }

    private HttpRequestMessage CreateRequest<T>(
        HttpMethod method,
        string endpoint,
        T body)
    {
        var request = new HttpRequestMessage(method, endpoint)
        {
            Content = JsonContent.Create(body, options: SerializerOptions)
        };
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _options.AccessToken.Trim());
        }

        return request;
    }

    private static JsonElement? GetFirstTicket(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data))
        {
            return null;
        }

        return data.ValueKind switch
        {
            JsonValueKind.Object => data,
            JsonValueKind.Array when data.GetArrayLength() > 0 => data[0],
            _ => null
        };
    }

    private static string? GetProviderError(JsonElement item)
    {
        return item.TryGetProperty("details", out var details)
               && details.ValueKind == JsonValueKind.Object
               && details.TryGetProperty("error", out var error)
            ? error.GetString()
            : null;
    }

    private static PushSendResult MapSendError(string? providerError)
    {
        return providerError switch
        {
            "DeviceNotRegistered" => new PushSendResult(
                PushSendOutcome.InvalidDevice,
                ErrorCode: "DEVICE_NOT_REGISTERED"),
            "MessageRateExceeded" => new PushSendResult(
                PushSendOutcome.RetryableFailure,
                ErrorCode: "MESSAGE_RATE_EXCEEDED"),
            "MessageTooBig" or "MismatchSenderId" or "InvalidCredentials" =>
                new PushSendResult(
                    PushSendOutcome.PermanentFailure,
                    ErrorCode: "PUSH_PROVIDER_REJECTED"),
            _ => new PushSendResult(
                PushSendOutcome.PermanentFailure,
                ErrorCode: ProviderRejected)
        };
    }

    private static PushReceiptResult MapReceipt(JsonElement receipt)
    {
        if (receipt.TryGetProperty("status", out var status)
            && string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
        {
            return new PushReceiptResult(PushReceiptOutcome.Delivered);
        }

        return GetProviderError(receipt) switch
        {
            "DeviceNotRegistered" => new PushReceiptResult(
                PushReceiptOutcome.DeviceNotRegistered,
                "DEVICE_NOT_REGISTERED"),
            "MessageRateExceeded" => new PushReceiptResult(
                PushReceiptOutcome.MessageRateExceeded,
                "MESSAGE_RATE_EXCEEDED"),
            _ => new PushReceiptResult(
                PushReceiptOutcome.PermanentFailure,
                ProviderRejected)
        };
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests
               || statusCode == HttpStatusCode.RequestTimeout
               || (int)statusCode >= 500;
    }

    private static PushReceiptBatchResult FailedReceiptBatch(
        bool retryable,
        string errorCode)
    {
        return new PushReceiptBatchResult(
            false,
            retryable,
            new Dictionary<string, PushReceiptResult>(),
            errorCode);
    }

    private sealed record ExpoSendRequest(
        string To,
        string Title,
        string Body,
        string Priority,
        string ChannelId,
        IReadOnlyDictionary<string, string> Data,
        int? Ttl);

    private sealed record ExpoReceiptRequest(
        IReadOnlyCollection<string> Ids);
}
