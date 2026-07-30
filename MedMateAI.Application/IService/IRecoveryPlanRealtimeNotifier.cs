using MedMateAI.Application.Models.RecoveryPlans;

namespace MedMateAI.Application.IService;

public interface IRecoveryPlanRealtimeNotifier
{
    Task TryNotifyRequestChangedAsync(
        RecoveryPlanRequestRealtimeNotification notification,
        CancellationToken cancellationToken);

    Task TryNotifyPlanChangedAsync(
        RecoveryPlanLifecycleRealtimeNotification notification,
        CancellationToken cancellationToken);

    Task TryNotifyDoctorRealtimeAccessChangedAsync(
        Guid doctorUserId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken);
}

public interface IRecoveryPlanRealtimeAccessService
{
    Task<RecoveryPlanRealtimeDoctorAccess?> GetDoctorAccessAsync(
        Guid doctorUserId,
        CancellationToken cancellationToken);
}
