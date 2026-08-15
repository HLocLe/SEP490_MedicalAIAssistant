using MedMateAI.Application.DTOs.RecoveryPlans.Analytics;
using MedMateAI.Application.Models.Analytics;

namespace MedMateAI.Application.IService;

public interface IRecoveryPlanFeedbackAnalyticsService
{
    Task<AnalyticsOperationResult<RecoveryPlanFeedbackAnalyticsResponse>> GetAsync(
        Guid doctorUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}
