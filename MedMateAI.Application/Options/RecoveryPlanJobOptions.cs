namespace MedMateAI.Application.Options;

public sealed class RecoveryPlanJobOptions
{
    public const string SectionName = "RecoveryPlanJobs";

    public bool OutboxProcessorEnabled { get; set; } = true;
    public bool NotificationEmailProcessorEnabled { get; set; } = true;
    public bool NotificationPushProcessorEnabled { get; set; } = true;
    public bool NotificationPushReceiptProcessorEnabled { get; set; } = true;
    public bool AssignmentTimeoutWorkerEnabled { get; set; } = true;
    public bool PlanCompletionWorkerEnabled { get; set; } = true;
    public bool MedicationReminderSchedulerEnabled { get; set; } = true;
    public int OutboxPollingSeconds { get; set; } = 5;
    public int NotificationPollingSeconds { get; set; } = 5;
    public int LifecyclePollingSeconds { get; set; } = 15;
    public int LifecycleBatchSize { get; set; } = 20;
    public int MedicationSchedulerPollingSeconds { get; set; } = 60;
    public int MedicationScheduleHorizonHours { get; set; } = 24;
    public int MedicationScheduleLookbackMinutes { get; set; } = 5;
    public int MedicationSchedulerBatchSize { get; set; } = 200;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
    public int ProcessingLeaseSeconds { get; set; } = 60;
    public int RetryBaseSeconds { get; set; } = 10;
    public int RetryMaxSeconds { get; set; } = 900;
    public int MedicationMaxLatenessMinutes { get; set; } = 30;

    // Preserve the Phase 5A environment key while the canonical option uses the shorter name.
    public int MedicationReminderMaxLatenessMinutes
    {
        get => MedicationMaxLatenessMinutes;
        set => MedicationMaxLatenessMinutes = value;
    }
}
