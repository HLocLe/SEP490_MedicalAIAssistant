namespace MedMateAI.Application.DTOs.ConsultationSessions.Requests;

public sealed class RegisterConsultationReminderRequest
{
    public bool EnableReminder { get; set; } = true;

    public string? PhoneNumber { get; set; }
}
