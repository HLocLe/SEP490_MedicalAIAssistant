namespace MedMateAI.Application.DTOs.ClinicalQuestions.Responses;

public sealed class ClinicalQuestionResponse
{
    public Guid Id { get; set; }

    public Guid? ChapterId { get; set; }

    public string? ChapterCode { get; set; }

    public string QuestionVi { get; set; } = string.Empty;

    public string? EnglishPrefix { get; set; }

    public int SortOrder { get; set; }

    public Dictionary<string, string> Answers { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
