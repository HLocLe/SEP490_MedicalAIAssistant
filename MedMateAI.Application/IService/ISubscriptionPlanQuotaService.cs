using MedMateAI.Application.DTOs.Quotas.Responses;
using MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Requests;
using MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Responses;
using MedMateAI.Application.Models.SubscriptionPlanQuotas;

namespace MedMateAI.Application.IService;

public interface ISubscriptionPlanQuotaService
{
    Task<SubscriptionPlanQuotaOperationResult<IReadOnlyList<QuotaResponse>>>
        ListQuotaDefinitionsAsync(CancellationToken cancellationToken = default);

    Task<SubscriptionPlanQuotaOperationResult<IReadOnlyList<SubscriptionPlanQuotaResponse>>>
        ListPlanQuotasAsync(
            Guid planId,
            CancellationToken cancellationToken = default);

    Task<SubscriptionPlanQuotaOperationResult<SubscriptionPlanQuotaResponse>>
        UpsertPlanQuotaAsync(
            Guid planId,
            Guid quotaId,
            UpsertSubscriptionPlanQuotaRequest request,
            CancellationToken cancellationToken = default);

    Task<SubscriptionPlanQuotaOperationResult<bool>> DeletePlanQuotaAsync(
        Guid planId,
        Guid quotaId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<SubscriptionPlanQuotaResponse>>>
        GetActivePlanQuotasAsync(
            IReadOnlyCollection<Guid> planIds,
            CancellationToken cancellationToken = default);
}
