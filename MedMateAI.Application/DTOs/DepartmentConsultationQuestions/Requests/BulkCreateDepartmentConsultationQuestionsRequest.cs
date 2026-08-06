namespace MedMateAI.Application.DTOs.DepartmentConsultationQuestions.Requests;

public sealed class BulkCreateDepartmentConsultationQuestionsRequest
{
    public List<CreateDepartmentConsultationQuestionRequest> Questions { get; set; } = new();
}
