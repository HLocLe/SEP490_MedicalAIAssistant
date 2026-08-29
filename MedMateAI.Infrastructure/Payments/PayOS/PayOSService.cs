using System.Text.Json;
using MedMateAI.Application.DTOs.Payments.PayOS;
using MedMateAI.Application.IService;
using MedMateAI.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace MedMateAI.Infrastructure.Payments.PayOS;

public sealed class PayOSService : IPayOSService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly PayOSOptions _options;
    private readonly MobilePaymentOptions _mobilePaymentOptions;
    private readonly PayOSClient _payOsClient;
    private readonly ILogger<PayOSService> _logger;

    public PayOSService(
        IOptions<PayOSOptions> options,
        IOptions<MobilePaymentOptions> mobilePaymentOptions,
        ILogger<PayOSService> logger)
    {
        _options = options.Value;
        _mobilePaymentOptions = mobilePaymentOptions.Value;
        _logger = logger;
        ValidateOptions(_options);
        _payOsClient = new PayOSClient(_options.ClientId, _options.ApiKey, _options.ChecksumKey);
    }

    public async Task<PayOSCreatePaymentResult> CreatePaymentLinkAsync(
        PayOSCreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentException("Request is required.");
        }

        if (request.OrderCode <= 0)
        {
            throw new ArgumentException("OrderCode must be greater than 0.");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than 0.");
        }

        var description = NormalizeDescription(request.Description);
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.");
        }

        var returnUrl = request.UseMobileCallbacks
            ? GetRequiredMobileCallbackUrl(
                _mobilePaymentOptions.ReturnUrl,
                nameof(MobilePaymentOptions.ReturnUrl))
            : string.IsNullOrWhiteSpace(request.ReturnUrl)
                ? _options.ReturnUrl
                : request.ReturnUrl;
        var cancelUrl = request.UseMobileCallbacks
            ? GetRequiredMobileCallbackUrl(
                _mobilePaymentOptions.CancelUrl,
                nameof(MobilePaymentOptions.CancelUrl))
            : string.IsNullOrWhiteSpace(request.CancelUrl)
                ? _options.CancelUrl
                : request.CancelUrl;

        var paymentRequest = new CreatePaymentLinkRequest
        {
            OrderCode = request.OrderCode,
            Amount = request.Amount,
            Description = description,
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl,
            ExpiredAt = DateTimeOffset.UtcNow
                .AddMinutes(_options.PaymentLinkExpirationMinutes)
                .ToUnixTimeSeconds(),
        };

        var requestOptions = new RequestOptions<CreatePaymentLinkRequest>
        {
            CancellationToken = cancellationToken,
        };
        var response = await _payOsClient.PaymentRequests.CreateAsync(
            paymentRequest,
            requestOptions);

        return new PayOSCreatePaymentResult
        {
            CheckoutUrl = response.CheckoutUrl,
            PaymentLinkId = response.PaymentLinkId,
            OrderCode = response.OrderCode,
            Status = response.Status.ToString().ToUpperInvariant(),
            RawResponse = JsonSerializer.Serialize(response),
        };
    }

    public async Task<PayOSPaymentLinkLookupResult> GetPaymentLinkAsync(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        if (orderCode <= 0)
        {
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.InvalidResponse);
        }

        var requestOptions = new RequestOptions
        {
            CancellationToken = cancellationToken,
        };

        return await ExecutePaymentLinkOperationAsync(
            orderCode,
            "lookup",
            () => _payOsClient.PaymentRequests.GetAsync(orderCode, requestOptions),
            cancellationToken);
    }

    public async Task<PayOSPaymentLinkLookupResult> CancelPaymentLinkAsync(
        long orderCode,
        string? cancellationReason,
        CancellationToken cancellationToken = default)
    {
        if (orderCode <= 0)
        {
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.InvalidResponse);
        }

        var reason = string.IsNullOrWhiteSpace(cancellationReason)
            ? "Cancelled by MediMate."
            : cancellationReason.Trim();
        var requestOptions = new RequestOptions<CancelPaymentLinkRequest>
        {
            CancellationToken = cancellationToken,
        };

        return await ExecutePaymentLinkOperationAsync(
            orderCode,
            "cancel",
            () => _payOsClient.PaymentRequests.CancelAsync(
                orderCode,
                reason,
                requestOptions),
            cancellationToken);
    }

    public async Task<PayOSWebhookResult> VerifyWebhookAsync(
        string rawBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return new PayOSWebhookResult
            {
                IsValid = false,
                RawBody = rawBody,
            };
        }

        Webhook? webhook;
        try
        {
            webhook = JsonSerializer.Deserialize<Webhook>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            return new PayOSWebhookResult
            {
                IsValid = false,
                RawBody = rawBody,
            };
        }

        if (webhook is null)
        {
            return new PayOSWebhookResult
            {
                IsValid = false,
                RawBody = rawBody,
            };
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var verifiedData = await _payOsClient.Webhooks.VerifyAsync(webhook);
            var (isCancelled, status, cancelFlag) = ParseCancelInfo(rawBody);

            var isPaid = webhook.Success
                && (string.Equals(webhook.Code, "00", StringComparison.Ordinal)
                    || string.Equals(verifiedData.Code, "00", StringComparison.Ordinal))
                && !string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase)
                && cancelFlag != true;

            if (!isCancelled && (string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase) || cancelFlag == true))
            {
                isCancelled = true;
            }

            return new PayOSWebhookResult
            {
                IsValid = true,
                IsPaid = isPaid,
                IsCancelled = isCancelled,
                OrderCode = verifiedData.OrderCode,
                Amount = Convert.ToInt32(verifiedData.Amount),
                Code = verifiedData.Code,
                Description = verifiedData.Description2,
                Reference = verifiedData.Reference,
                PaymentLinkId = verifiedData.PaymentLinkId,
                Currency = verifiedData.Currency,
                RawBody = rawBody,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "payOS webhook verification failed with category {ErrorCategory}.",
                ex.GetType().Name);
            return new PayOSWebhookResult
            {
                IsValid = false,
                RawBody = rawBody,
            };
        }
    }

    private async Task<PayOSPaymentLinkLookupResult> ExecutePaymentLinkOperationAsync(
        long orderCode,
        string operation,
        Func<Task<PaymentLink>> providerOperation,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await providerOperation();
            var status = MapStatus(response.Status);

            if (!IsValidPaymentLink(response, status))
            {
                LogPaymentLinkFailure(orderCode, operation, "InvalidResponse", null);
                return PayOSPaymentLinkLookupResult.Fail(
                    PayOSPaymentLinkLookupError.InvalidResponse);
            }

            var latestTransaction = response.Transactions?.LastOrDefault();
            var rawResponse = JsonSerializer.Serialize(response);

            _logger.LogInformation(
                "payOS payment link {Operation} completed for order {OrderCode} with status {ProviderStatus}.",
                operation,
                orderCode,
                status);

            return PayOSPaymentLinkLookupResult.Ok(new PayOSPaymentLinkResult
            {
                OrderCode = response.OrderCode,
                PaymentLinkId = response.Id,
                Amount = response.Amount,
                AmountPaid = response.AmountPaid,
                AmountRemaining = response.AmountRemaining,
                Status = status!,
                LatestTransactionReference = latestTransaction?.Reference,
                LatestTransactionDescription = latestTransaction?.Description,
                RawResponse = rawResponse,
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UserAbortException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (UserAbortException ex)
        {
            LogPaymentLinkFailure(orderCode, operation, "Unavailable", ex.StatusCode);
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.Unavailable);
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
        {
            LogPaymentLinkFailure(orderCode, operation, "NotFound", ex.StatusCode);
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.NotFound);
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status429TooManyRequests)
        {
            LogPaymentLinkFailure(orderCode, operation, "RateLimited", ex.StatusCode);
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.RateLimited);
        }
        catch (ApiException ex) when (
            ex.StatusCode is null
            or StatusCodes.Status408RequestTimeout
            or >= 500)
        {
            LogPaymentLinkFailure(orderCode, operation, "Unavailable", ex.StatusCode);
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.Unavailable);
        }
        catch (ApiException ex)
        {
            LogPaymentLinkFailure(orderCode, operation, "InvalidResponse", ex.StatusCode);
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.InvalidResponse);
        }
        catch (HttpRequestException ex)
        {
            LogPaymentLinkFailure(orderCode, operation, ex.GetType().Name, null);
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.Unavailable);
        }
        catch (TaskCanceledException ex)
        {
            LogPaymentLinkFailure(orderCode, operation, ex.GetType().Name, null);
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.Unavailable);
        }
        catch (JsonException ex)
        {
            LogPaymentLinkFailure(orderCode, operation, ex.GetType().Name, null);
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.InvalidResponse);
        }
        catch (NotSupportedException ex)
        {
            LogPaymentLinkFailure(orderCode, operation, ex.GetType().Name, null);
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.InvalidResponse);
        }
        catch (Exception ex)
        {
            LogPaymentLinkFailure(orderCode, operation, ex.GetType().Name, null);
            return PayOSPaymentLinkLookupResult.Fail(
                PayOSPaymentLinkLookupError.Unavailable);
        }
    }

    private static string? MapStatus(PaymentLinkStatus status)
    {
        return status switch
        {
            PaymentLinkStatus.Pending => "PENDING",
            PaymentLinkStatus.Processing => "PROCESSING",
            PaymentLinkStatus.Paid => "PAID",
            PaymentLinkStatus.Cancelled => "CANCELLED",
            PaymentLinkStatus.Underpaid => "UNDERPAID",
            PaymentLinkStatus.Expired => "EXPIRED",
            PaymentLinkStatus.Failed => "FAILED",
            _ => null,
        };
    }

    private static bool IsValidPaymentLink(
        PaymentLink? paymentLink,
        string? status)
    {
        return paymentLink is not null
            && paymentLink.OrderCode > 0
            && !string.IsNullOrWhiteSpace(paymentLink.Id)
            && paymentLink.Amount > 0
            && paymentLink.AmountPaid >= 0
            && paymentLink.AmountRemaining >= 0
            && status is not null;
    }

    private void LogPaymentLinkFailure(
        long orderCode,
        string operation,
        string errorCategory,
        int? providerStatusCode)
    {
        _logger.LogWarning(
            "payOS payment link {Operation} failed for order {OrderCode}; category {ErrorCategory}; provider status {ProviderStatusCode}.",
            operation,
            orderCode,
            errorCategory,
            providerStatusCode);
    }

    private static void ValidateOptions(PayOSOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new InvalidOperationException("PayOS:ClientId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("PayOS:ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ChecksumKey))
        {
            throw new InvalidOperationException("PayOS:ChecksumKey is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ReturnUrl))
        {
            throw new InvalidOperationException("PayOS:ReturnUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(options.CancelUrl))
        {
            throw new InvalidOperationException("PayOS:CancelUrl is required.");
        }
    }

    private static string GetRequiredMobileCallbackUrl(
        string? value,
        string optionName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"MobilePayment:{optionName} is required for mobile checkout.");
        }

        var normalized = value.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp
                && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"MobilePayment:{optionName} must be an absolute HTTP or HTTPS URL for mobile checkout.");
        }

        return normalized;
    }

    private static string NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var normalized = description.Trim();
        if (normalized.Length <= 25)
        {
            return normalized;
        }

        return normalized[..25];
    }

    private static (bool IsCancelled, string? Status, bool? CancelFlag) ParseCancelInfo(string rawBody)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            var data = root.TryGetProperty("data", out var dataElement)
                ? dataElement
                : root;

            string? status = null;
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("status", out var statusElement))
            {
                status = statusElement.GetString();
            }

            bool? cancel = null;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("cancel", out var cancelElement))
            {
                cancel = cancelElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String when bool.TryParse(cancelElement.GetString(), out var parsed) => parsed,
                    _ => null,
                };
            }

            var isCancelled =
                string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase)
                || cancel == true;

            return (isCancelled, status, cancel);
        }
        catch
        {
            return (false, null, null);
        }
    }
}
