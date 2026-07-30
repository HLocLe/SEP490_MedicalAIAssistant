namespace MedMateAI.Application.Options;

public sealed class RecoveryPlanJobOptions
{
    public const string SectionName = "RecoveryPlanJobs";

    public bool OutboxProcessorEnabled { get; set; } = true;
    public bool NotificationEmailProcessorEnabled { get; set; } = true;
    public bool AssignmentTimeoutWorkerEnabled { get; set; } = true;
    public bool PlanCompletionWorkerEnabled { get; set; } = true;
    public int OutboxPollingSeconds { get; set; } = 5;
    public int NotificationPollingSeconds { get; set; } = 5;
    public int LifecyclePollingSeconds { get; set; } = 15;
    public int LifecycleBatchSize { get; set; } = 20;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
    public int ProcessingLeaseSeconds { get; set; } = 60;
    public int RetryBaseSeconds { get; set; } = 10;
    public int RetryMaxSeconds { get; set; } = 900;
    public int MedicationReminderMaxLatenessMinutes { get; set; } = 30;
}
