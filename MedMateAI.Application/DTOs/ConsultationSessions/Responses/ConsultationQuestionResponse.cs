namespace MedMateAI.Application.DTOs.ConsultationSessions.Responses;

public sealed class ConsultationQuestionResponse
{
    public Guid Id { get; set; }

    public string? QuestionText { get; set; }

    public string? Category { get; set; }

    public int Priority { get; set; }
}
