using MedMateAI.Application.DTOs.ChecklistItems.Responses;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.ConsultationSessions.Responses;

public sealed class ConsultationSummaryUserInfoResponse
{
    public string DisplayName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }
}

public sealed class ConsultationSummaryResponse
{
    public Guid SessionId { get; set; }

    public ConsultationSummaryUserInfoResponse User { get; set; } = new();

    public Guid DepartmentId { get; set; }

    public string DepartmentName { get; set; } = string.Empty;

    public Guid? FacilityId { get; set; }

    public string? FacilityName { get; set; }

    public DateTime? AppointmentTime { get; set; }

    public string Symptoms { get; set; } = string.Empty;

    public ConsultationSessionStatus Status { get; set; }

    public bool IsReminderEnabled { get; set; }

    public bool ReminderSmsSent { get; set; }

    public IReadOnlyList<ChecklistItemResponse> ChecklistItems { get; set; } = [];

    public IReadOnlyList<ConsultationQuestionResponse> Questions { get; set; } = [];
}
