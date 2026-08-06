using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Requests;

public sealed class CreateDepartmentConsultationQuestionRequest
{
    public Guid DepartmentId { get; set; }

    public ConsultationQuestionCategory Category { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
