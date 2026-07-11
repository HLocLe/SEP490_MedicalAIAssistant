namespace MedMateAI.Application.IService;

public interface ITranslationService
{
    Task<string> TranslateToEnglishAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> TranslateBatchToVietnameseAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
