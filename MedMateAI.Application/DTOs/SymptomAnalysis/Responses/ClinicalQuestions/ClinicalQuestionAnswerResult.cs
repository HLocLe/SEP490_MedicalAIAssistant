namespace MedMateAI.Application.DTOs.SymptomAnalysis.Responses.ClinicalQuestions;

public sealed class ClinicalQuestionAnswerResult
{
    public Guid QuestionId { get; set; }

    public string? EnglishPrefix { get; set; }

    public string? QuestionVi { get; set; }

    public Dictionary<string, bool> Answers { get; set; } = new();
}
