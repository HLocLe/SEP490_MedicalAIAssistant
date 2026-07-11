namespace MedMateAI.Application.DTOs.ConsultationSessions.Requests;

public sealed class GenerateConsultationQuestionsRequest
{
    public Guid DepartmentId { get; set; }

    public string Symptoms { get; set; } = string.Empty;
}
