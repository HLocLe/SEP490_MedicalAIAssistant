namespace MedMateAI.Domain.Entities;

public sealed class Notification : BaseEntity
{
    public Guid UserId { get; set; }

    public Guid? ReminderId { get; set; }

    public string? Title { get; set; }

    public string? Message { get; set; }

    public string? Channel { get; set; }

    public string? Status { get; set; }

    public DateTime? SentAt { get; set; }

    public string NotificationType { get; set; } = "FOLLOW_UP_REMINDER";

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public DateTime? NextAttemptAt { get; set; }

    public int AttemptCount { get; set; }

    public int ReceiptAttemptCount { get; set; }

    public Guid? PushDeviceId { get; set; }

    public string? ProviderMessageId { get; set; }

    public DateTime? ProviderSubmittedAt { get; set; }

    public int? ProviderPushTokenVersion { get; set; }

    public string? LastError { get; set; }

    public string? DedupeKey { get; set; }

    public FollowUpReminder? Reminder { get; set; }

    public UserPushDevice? PushDevice { get; set; }
}
