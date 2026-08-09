using System.Globalization;
using System.Text;

namespace MedMateAI.Application.Common;

public static class ConsultationReminderSmsBuilder
{
    public static string Build(
        string displayName,
        DateOnly? dateOfBirth,
        string phoneNumber,
        string departmentName,
        string facilityName,
        DateTime? appointmentTime)
    {
        var dob = dateOfBirth?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Chua cap nhat";
        var appointment = "Chua cap nhat";
        if (appointmentTime.HasValue)
        {
            // Vietnam is fixed UTC+7 (no DST). Format SMS in local VN time.
            var vietnamTime = appointmentTime.Value.ToUniversalTime().AddHours(7);
            appointment = vietnamTime.ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        var rawContent =
            $"[MedMateAI] Chao {displayName.Trim()} ({dob}), SDT {phoneNumber.Trim()}. " +
            $"Ban co lich hen kham tai Khoa {departmentName.Trim()} - {facilityName.Trim()} vao luc {appointment}. " +
            "Vui long dang nhap website MedMateAI de xem chi tiet danh sach can chuan bi va 6 cau hoi AI goi y cho bac si.";

        return RemoveVietnameseAccents(rawContent);
    }

    private static string RemoveVietnameseAccents(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D');
    }
}
