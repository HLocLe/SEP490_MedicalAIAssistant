using MedMateAI.Application.Models.ServiceCredits;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.IService;

public interface ISymptomAnalysisQuotaService
{
    Task<ServiceCreditOperationResult<UserSubscriptionUsage>> ReserveAsync(
        Guid userId,
        Guid sessionId,
        Guid actorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task FinalizeAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
