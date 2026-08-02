using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Payments.Responses;
using MedMateAI.Application.Models.Payments;

namespace MedMateAI.Application.IService;

public interface IPaymentService
{
    Task<PayOSReturnResponse> ProcessPayOSReturnAsync(
        IReadOnlyDictionary<string, string> queryParameters,
        CancellationToken cancellationToken = default);

    Task<PayOSReturnResponse> ProcessPayOSCancelAsync(
        IReadOnlyDictionary<string, string> queryParameters,
        CancellationToken cancellationToken = default);

    Task<bool> ProcessPayOSWebhookAsync(
        string rawBody,
        CancellationToken cancellationToken = default);

    Task<PayOSPaymentStatusResponse?> GetPayOSPaymentStatusAsync(
        long orderCode,
        CancellationToken cancellationToken = default);

    Task<PaymentReconciliationResult<PayOSPaymentStatusResponse>>
        ReconcilePayOSPaymentAsync(
            long orderCode,
            CancellationToken cancellationToken = default);

    Task<PagedResponse<PaymentResponse>> GetAllPaymentsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<PaymentResponse>> GetPaymentsByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, PagedResponse<PaymentResponse>? Data)> GetMyPaymentsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, PaymentResponse? Data)> GetMyPaymentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse?> GetPaymentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
