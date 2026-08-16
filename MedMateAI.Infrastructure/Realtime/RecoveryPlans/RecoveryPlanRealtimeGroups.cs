namespace MedMateAI.Infrastructure.Realtime.RecoveryPlans;

internal static class RecoveryPlanRealtimeGroups
{
    public const string DoctorQueue = "recovery-plan:doctor-queue";

    public static string User(Guid userId)
    {
        return $"recovery-plan:user:{userId:N}";
    }

    public static string Doctor(Guid doctorId)
    {
        return $"recovery-plan:doctor:{doctorId:N}";
    }
}
