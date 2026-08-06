using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Responses;

public sealed class DepartmentConsultationQuestionResponse
{
    public Guid Id { get; set; }

    public Guid DepartmentId { get; set; }

    public ConsultationQuestionCategory Category { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
