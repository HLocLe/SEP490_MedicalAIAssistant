namespace MedMateAI.Application.DTOs.SymptomAnalysis.Requests;

public sealed class ClinicalQuestionAnswerItem
{
    public Guid QuestionId { get; set; }

    public Dictionary<string, bool> Answers { get; set; } = new();
}
