using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Requests;

public sealed class UpdateDepartmentConsultationQuestionRequest
{
    public Guid? DepartmentId { get; set; }

    public ConsultationQuestionCategory? Category { get; set; }

    public string? QuestionText { get; set; }

    public int? SortOrder { get; set; }

    public bool? IsActive { get; set; }
}
