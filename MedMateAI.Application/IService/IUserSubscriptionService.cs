using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.UserSubscriptions.Requests;
using MedMateAI.Application.DTOs.UserSubscriptions.Responses;

namespace MedMateAI.Application.IService;

public interface IUserSubscriptionService
{
    Task<(bool Succeeded, IEnumerable<string> Errors, CheckoutSubscriptionResponse? Data)> CheckoutAsync(
        CheckoutSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSubscriptionResponse>> GetMySubscriptionsAsync(
        CancellationToken cancellationToken = default);

    Task<UserSubscriptionResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, UserSubscriptionResponse? Data)> CancelAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, PagedResponse<UserSubscriptionResponse>? Data)>
        GetAdminSubscriptionsAsync(
            int pageNumber,
            int pageSize,
            string? status,
            bool currentOnly,
            CancellationToken cancellationToken = default);
}
