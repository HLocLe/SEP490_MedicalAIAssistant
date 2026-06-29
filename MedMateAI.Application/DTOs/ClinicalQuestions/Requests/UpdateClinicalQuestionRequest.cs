namespace MedMateAI.Application.DTOs.ClinicalQuestions.Requests;

public sealed class UpdateClinicalQuestionRequest
{
    public Guid? ChapterId { get; set; }

    public string? QuestionVi { get; set; }

    public string? EnglishPrefix { get; set; }

    public int? SortOrder { get; set; }

    public Dictionary<string, string>? Answers { get; set; }
}
