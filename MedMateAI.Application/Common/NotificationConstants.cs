namespace MedMateAI.Application.Common;

public static class NotificationChannels
{
    public const string Email = "Email";
}

public static class NotificationStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public static class NotificationTypes
{
    public const string RecoveryPlanReady = "RecoveryPlanReady";
    public const string RecoveryPlanCompleted = "RecoveryPlanCompleted";
    public const string MedicationReminder = "MedicationReminder";
}

public static class NotificationReferenceTypes
{
    public const string RecoveryPlan = "RecoveryPlan";
    public const string UserMedicationReminderTime = "UserMedicationReminderTime";
}

public static class RecoveryPlanNotificationContent
{
    public const string ReadyTitle = "Kế hoạch hồi phục đã sẵn sàng";
    public const string ReadyMessage =
        "Kế hoạch hồi phục của bạn đã sẵn sàng. Vui lòng đăng nhập để xem và bắt đầu.";
    public const string CompletedTitle = "Kế hoạch hồi phục đã kết thúc";
    public const string CompletedMessage =
        "Kế hoạch hồi phục của bạn đã kết thúc. Vui lòng đăng nhập để xem hướng dẫn tái kiểm tra sức khỏe.";
}
