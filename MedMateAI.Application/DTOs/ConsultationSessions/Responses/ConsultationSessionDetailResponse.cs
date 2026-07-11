using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.ConsultationSessions.Responses;

public sealed class ConsultationSessionDetailResponse
{
    public Guid SessionId { get; set; }

    public Guid DepartmentId { get; set; }

    public string DepartmentName { get; set; } = string.Empty;

    public string Symptoms { get; set; } = string.Empty;

    public ConsultationSessionStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public IReadOnlyList<ConsultationQuestionResponse> Questions { get; set; } = [];
}
