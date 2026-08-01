using MedMateAI.Application.IService;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class LabTestOcrProcessor : ILabTestOcrProcessor
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly ILabTestResultAnalyzer _resultAnalyzer;
    private readonly ILogger<LabTestOcrProcessor> _logger;

    public LabTestOcrProcessor(
        IUnitOfWork unitOfWork,
        IDocumentIntelligenceService documentIntelligenceService,
        ILabTestResultAnalyzer resultAnalyzer,
        ILogger<LabTestOcrProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _documentIntelligenceService = documentIntelligenceService;
        _resultAnalyzer = resultAnalyzer;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _unitOfWork.LabTestSessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            _logger.LogWarning("Lab test OCR job skipped because session {SessionId} was not found.", sessionId);
            return;
        }

        if (session.Status is LabTestSessionStatus.Completed or LabTestSessionStatus.Failed)
        {
            _logger.LogInformation(
                "Lab test OCR job skipped because session {SessionId} is already in status {Status}.",
                sessionId,
                session.Status);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.DocumentUrl))
        {
            session.Status = LabTestSessionStatus.Failed;
            session.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.LabTestSessions.Update(session);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var rawOcrText = await _documentIntelligenceService.AnalyzeFromUrlAsync(
                session.DocumentUrl,
                cancellationToken);

            session.RawOcrText = rawOcrText;
            session.Status = LabTestSessionStatus.Completed;
            session.ProcessedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.LabTestSessions.Update(session);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _resultAnalyzer.AnalyzeAndPersistAsync(sessionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lab test OCR failed for session {SessionId}.", sessionId);

            session.Status = LabTestSessionStatus.Failed;
            session.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.LabTestSessions.Update(session);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
