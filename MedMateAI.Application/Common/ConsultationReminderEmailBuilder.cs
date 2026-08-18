using System.Globalization;
using System.Net;

namespace MedMateAI.Application.Common;

public static class ConsultationReminderEmailBuilder
{
    public const string Subject = "Nhắc lịch khám MedMateAI";

    public static string BuildHtml(
        string displayName,
        DateOnly? dateOfBirth,
        string departmentName,
        string facilityName,
        DateTime? appointmentTime)
    {
        var dob = dateOfBirth?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Chưa cập nhật";
        var appointment = "Chưa cập nhật";
        if (appointmentTime.HasValue)
        {
            var vietnamTime = appointmentTime.Value.ToUniversalTime().AddHours(7);
            appointment = vietnamTime.ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        var name = Encode(string.IsNullOrWhiteSpace(displayName) ? "Bạn" : displayName.Trim());
        var department = Encode(string.IsNullOrWhiteSpace(departmentName) ? "Chưa cập nhật" : departmentName.Trim());
        var facility = Encode(string.IsNullOrWhiteSpace(facilityName) ? "Chưa cập nhật" : facilityName.Trim());

        return $"""
            <p>Chào {name} (ngày sinh: {Encode(dob)}),</p>
            <p>Bạn có lịch hẹn khám tại <strong>Khoa {department}</strong> - {facility} vào lúc <strong>{Encode(appointment)}</strong>.</p>
            <p>Vui lòng đăng nhập website MedMateAI để xem chi tiết danh sách cần chuẩn bị và 6 câu hỏi AI gợi ý cho bác sĩ.</p>
            """;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
