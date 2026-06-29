namespace MedMateAI.Application.DTOs.IcdChapters.Requests;

public sealed class UpdateIcdChapterRequest
{
    public string? ChapterCode { get; set; }

    public string? ChapterName { get; set; }

    public Dictionary<string, int>? KeywordWeights { get; set; }
}
