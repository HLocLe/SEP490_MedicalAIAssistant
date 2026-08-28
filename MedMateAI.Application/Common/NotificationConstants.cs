namespace MedMateAI.Application.Common;

public static class NotificationChannels
{
    public const string Email = "Email";
    public const string Push = "Push";
}

public static class NotificationStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Submitted = "Submitted";
    public const string ReceiptProcessing = "ReceiptProcessing";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public static class PushNotificationContent
{
    public const string RecoveryPlanReadyTitle = "Kế hoạch hồi phục đã sẵn sàng";
    public const string RecoveryPlanReadyMessage =
        "Kế hoạch hồi phục của bạn đã sẵn sàng. Nhấn để xem chi tiết.";
    public const string RecoveryPlanCompletedTitle = "Kế hoạch hồi phục đã kết thúc";
    public const string RecoveryPlanCompletedMessage =
        "Kế hoạch hồi phục của bạn đã kết thúc. Nhấn để xem thông tin.";
    public const string RecoveryPlanCancelledTitle = "Kế hoạch hồi phục đã được hủy";
    public const string RecoveryPlanCancelledMessage =
        "Kế hoạch hồi phục của bạn đã được hủy. Nhấn để xem thông tin.";
    public const string MedicationReminderTitle = "Nhắc lịch dùng thuốc";
    public const string MedicationReminderMessage =
        "Đã đến thời gian dùng thuốc theo lịch bạn đã lưu.";
}

public static class NotificationTypes
{
    public const string RecoveryPlanReady = "RecoveryPlanReady";
    public const string RecoveryPlanCompleted = "RecoveryPlanCompleted";
    public const string RecoveryPlanCancelled = "RecoveryPlanCancelled";
    public const string MedicationReminder = "MedicationReminder";
    public const string ConsultationReminder = "ConsultationReminder";
}

public static class NotificationReferenceTypes
{
    public const string RecoveryPlan = "RecoveryPlan";
    public const string UserMedicationReminderTime = "UserMedicationReminderTime";
    public const string ConsultationSession = "ConsultationSession";
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

public static class RecoveryPlanCancellationNotificationContent
{
    public const string Title = "Xác nhận hủy kế hoạch hồi phục";
    public const string Message =
        "Kế hoạch hồi phục đã được hủy theo yêu cầu và vẫn được lưu trong lịch sử của bạn.";
}

public static class MedicationReminderNotificationContent
{
    public const string Title = "Nhắc lịch dùng thuốc";
    public const string Message =
        "Bạn có một lịch nhắc dùng thuốc dựa trên thông tin đã cung cấp.";
    public const string Disclaimer =
        "Đây là lịch nhắc dựa trên thông tin bạn đã cung cấp. Hệ thống không kê đơn hoặc xác minh chỉ định dùng thuốc.";
}
