using MedMateAI.Application.DTOs.Payments.PayOS;

namespace MedMateAI.Application.IService;

public interface IPayOSService
{
    Task<PayOSCreatePaymentResult> CreatePaymentLinkAsync(
        PayOSCreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PayOSPaymentLinkLookupResult> GetPaymentLinkAsync(
        long orderCode,
        CancellationToken cancellationToken = default);

    Task<PayOSPaymentLinkLookupResult> CancelPaymentLinkAsync(
        long orderCode,
        string? cancellationReason,
        CancellationToken cancellationToken = default);

    Task<PayOSWebhookResult> VerifyWebhookAsync(
        string rawBody,
        CancellationToken cancellationToken = default);
}
