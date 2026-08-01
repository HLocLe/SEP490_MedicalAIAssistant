namespace MedMateAI.Application.IService;

public interface ILabTestResultAnalyzer
{
    Task AnalyzeAndPersistAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
