using System.Globalization;

namespace MedMateAI.Application.Common;

public static class ConsultationReminderPushBuilder
{
    public const string Title = "Nhắc lịch khám MedMateAI";

    public static string BuildBody(
        string departmentName,
        string facilityName,
        DateTime? appointmentTime)
    {
        var department = string.IsNullOrWhiteSpace(departmentName)
            ? "Chưa cập nhật"
            : departmentName.Trim();
        var facility = string.IsNullOrWhiteSpace(facilityName)
            ? "Chưa cập nhật"
            : facilityName.Trim();
        var appointment = FormatAppointmentVietnam(appointmentTime);

        return $"Bạn có lịch hẹn khám tại Khoa {department} - {facility} vào lúc {appointment}. "
               + "Mở MedMateAI để xem danh sách chuẩn bị và câu hỏi gợi ý cho bác sĩ.";
    }

    public static IReadOnlyDictionary<string, string> BuildData(Guid sessionId)
    {
        return new Dictionary<string, string>
        {
            ["notificationType"] = NotificationTypes.ConsultationReminder,
            ["referenceType"] = NotificationReferenceTypes.ConsultationSession,
            ["referenceId"] = sessionId.ToString("D"),
        };
    }

    public static int? BuildTimeToLiveSeconds(DateTime? appointmentTimeUtc)
    {
        if (!appointmentTimeUtc.HasValue)
        {
            return null;
        }

        var appointmentUtc = appointmentTimeUtc.Value.ToUniversalTime();
        var remaining = appointmentUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return null;
        }

        var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        return seconds > 0 ? seconds : null;
    }

    private static string FormatAppointmentVietnam(DateTime? appointmentTime)
    {
        if (!appointmentTime.HasValue)
        {
            return "Chưa cập nhật";
        }

        var vietnamTime = appointmentTime.Value.ToUniversalTime().AddHours(7);
        return vietnamTime.ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
    }
}
