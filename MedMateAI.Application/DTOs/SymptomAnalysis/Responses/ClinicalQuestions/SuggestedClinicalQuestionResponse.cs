namespace MedMateAI.Application.DTOs.SymptomAnalysis.Responses.ClinicalQuestions;

public sealed class SuggestedClinicalQuestionResponse
{
    public Guid QuestionId { get; set; }

    public string QuestionVi { get; set; } = string.Empty;

    public Guid? ChapterId { get; set; }

    public string? ChapterCode { get; set; }

    public int TotalScore { get; set; }

    public List<string> MatchedKeywords { get; set; } = new();

    public Dictionary<string, string> Answers { get; set; } = new();
}
