namespace MedMateAI.Application.Common;

public static class RecoveryPlanRealtimeConstants
{
    public const string HubPath = "/hubs/recovery-plans";
    public const string DoctorRole = "Doctor";
    public const string DoctorAccessChangedEventType = "RecoveryPlanRealtimeAccessChanged";
    public const string ConfigurationSectionName = "RecoveryPlanRealtime";
    public const string UseRedisBackplaneKey = "UseRedisBackplane";
    public const string RedisConnectionStringKey = "Redis:ConnectionString";
    public const string RedisChannelPrefix = "medmateai:recovery-plan";
    public const int DefaultDeliveryTimeoutSeconds = 3;
}
