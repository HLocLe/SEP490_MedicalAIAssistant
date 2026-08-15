using MedMateAI.Application.Models.RecoveryPlans.Analytics;

namespace MedMateAI.Application.IRepository;

public interface IRecoveryPlanFeedbackAnalyticsRepository
{
    Task<RecoveryPlanFeedbackAnalyticsData?> GetAnalyticsAsync(
        Guid doctorUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}
