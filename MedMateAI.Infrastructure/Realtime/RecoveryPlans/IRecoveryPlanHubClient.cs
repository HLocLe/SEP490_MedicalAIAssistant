using MedMateAI.Application.Models.RecoveryPlans;

namespace MedMateAI.Infrastructure.Realtime.RecoveryPlans;

public interface IRecoveryPlanHubClient
{
    Task RecoveryPlanQueueChanged(RecoveryPlanQueueChangedMessage message);

    Task RecoveryPlanRequestChanged(RecoveryPlanRequestChangedMessage message);

    Task RecoveryPlanChanged(RecoveryPlanChangedMessage message);

    Task RecoveryPlanRealtimeAccessChanged(
        RecoveryPlanRealtimeAccessChangedMessage message);
}
