namespace MedMateAI.Domain.Entities;

public sealed class ClinicalQuestion : BaseEntity
{
    public Guid? ChapterId { get; set; }

    public string? ChapterCode { get; set; }

    public string QuestionVi { get; set; } = string.Empty;

    public string? EnglishPrefix { get; set; }

    public int SortOrder { get; set; }

    public Dictionary<string, string> Answers { get; set; } = new();

    public ICollection<SessionClinicalQuestionAnswer> SessionAnswers { get; set; } =
        new List<SessionClinicalQuestionAnswer>();

    public IcdChapter? IcdChapter { get; set; } = null;
}
