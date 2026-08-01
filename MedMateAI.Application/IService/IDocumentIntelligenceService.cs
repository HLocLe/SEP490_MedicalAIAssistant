namespace MedMateAI.Application.IService;

public interface IDocumentIntelligenceService
{
    Task<string> AnalyzeFromUrlAsync(
        string documentUrl,
        CancellationToken cancellationToken = default);

    Task<string> AnalyzeFromStreamAsync(
        Stream documentStream,
        string contentType,
        CancellationToken cancellationToken = default);
}
