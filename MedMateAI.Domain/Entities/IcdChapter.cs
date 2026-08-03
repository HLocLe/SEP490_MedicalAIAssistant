namespace MedMateAI.Domain.Entities;

public sealed class IcdChapter : BaseEntity
{
    public string ChapterCode { get; set; } = string.Empty;

    public string ChapterName { get; set; } = string.Empty;

    public Dictionary<string, int> KeywordWeights { get; set; } = new();

    public ICollection<ClinicalQuestion> ClinicalQuestions { get; set; } = new List<ClinicalQuestion>();

    public ICollection<MedicalDepartment> MedicalDepartments { get; set; } = new List<MedicalDepartment>();
}
