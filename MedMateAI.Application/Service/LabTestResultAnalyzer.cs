using MedMateAI.Application.Helpers;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class LabTestResultAnalyzer : ILabTestResultAnalyzer
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILabTestOcrStructurer _ocrStructurer;
    private readonly ILogger<LabTestResultAnalyzer> _logger;

    public LabTestResultAnalyzer(
        IUnitOfWork unitOfWork,
        ILabTestOcrStructurer ocrStructurer,
        ILogger<LabTestResultAnalyzer> logger)
    {
        _unitOfWork = unitOfWork;
        _ocrStructurer = ocrStructurer;
        _logger = logger;
    }

    public async Task AnalyzeAndPersistAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _unitOfWork.LabTestSessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.RawOcrText))
        {
            return;
        }

        var alreadyAnalyzed = await _unitOfWork.LabTestResultDetails.FirstOrDefaultAsync(
            x => x.TestSessionId == sessionId,
            cancellationToken: cancellationToken);

        if (alreadyAnalyzed is not null)
        {
            return;
        }

        var parsedRows = await _ocrStructurer.StructureAsync(session.RawOcrText, cancellationToken);
        if (parsedRows.Count == 0)
        {
            _logger.LogWarning("No lab rows parsed from OCR text for session {SessionId}.", sessionId);
            return;
        }

        var indicators = await _unitOfWork.LabIndicators.GetAllActiveWithDetailsAsync(cancellationToken);
        var ageGroup = ResolveAgeGroup(session.PatientAgeAtTest);
        var utcNow = DateTime.UtcNow;

        for (var rowIndex = 0; rowIndex < parsedRows.Count; rowIndex++)
        {
            var row = parsedRows[rowIndex];

            var ocrExtract = new LabTestOcrExtract
            {
                Id = Guid.NewGuid(),
                TestSessionId = sessionId,
                RowIndex = rowIndex,
                ExtractedTestName = row.TestName,
                ExtractedValue = row.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ExtractedReferenceText = row.ReferenceText,
                CreatedAt = utcNow,
            };

            _unitOfWork.LabTestOcrExtracts.Add(ocrExtract);

            var match = LabTestIndicatorMatcher.Match(row.TestName, indicators);
            if (match is null || !row.Value.HasValue)
            {
                _unitOfWork.LabTestResultDetails.Add(new LabTestResultDetail
                {
                    Id = Guid.NewGuid(),
                    TestSessionId = sessionId,
                    RawExtractedName = row.TestName,
                    RawExtractedValue = row.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    UserValue = row.Value,
                    Status = LabResultStatus.Unknown,
                    IsMatched = false,
                    CreatedAt = utcNow,
                });
                continue;
            }

            var indicator = match.Indicator;
            var referenceRange = LabResultEvaluator.SelectReferenceRange(
                indicator.LabIndicatorReferenceRanges,
                session.PatientGenderAtTest,
                ageGroup);

            LabResultStatus status;
            double? referenceMinUsed;
            double? referenceMaxUsed;
            string? referenceUnitUsed;
            ReferenceComparisonType? comparisonTypeUsed;
            double? deviationPercent;

            if (referenceRange is not null)
            {
                status = LabResultEvaluator.Evaluate(row.Value.Value, referenceRange);
                referenceMinUsed = referenceRange.MinValue;
                referenceMaxUsed = referenceRange.MaxValue;
                referenceUnitUsed = referenceRange.Unit ?? indicator.Unit;
                comparisonTypeUsed = referenceRange.ComparisonType;
                deviationPercent = LabResultEvaluator.CalculateDeviationPercent(
                    row.Value.Value,
                    referenceRange.ComparisonType,
                    referenceRange.MinValue,
                    referenceRange.MaxValue);
            }
            else
            {
                status = LabResultEvaluator.Evaluate(
                    row.Value.Value,
                    indicator,
                    session.PatientGenderAtTest,
                    ageGroup);
                referenceMinUsed = indicator.MinReference;
                referenceMaxUsed = indicator.MaxReference;
                referenceUnitUsed = indicator.Unit;
                comparisonTypeUsed = indicator.MinReference.HasValue && indicator.MaxReference.HasValue
                    ? ReferenceComparisonType.Between
                    : indicator.MaxReference.HasValue && !indicator.MinReference.HasValue
                        ? ReferenceComparisonType.LessThanOrEqual
                        : indicator.MinReference.HasValue && !indicator.MaxReference.HasValue
                            ? ReferenceComparisonType.GreaterThanOrEqual
                            : null;
                deviationPercent = comparisonTypeUsed.HasValue
                    ? LabResultEvaluator.CalculateDeviationPercent(
                        row.Value.Value,
                        comparisonTypeUsed.Value,
                        referenceMinUsed,
                        referenceMaxUsed)
                    : null;
            }

            var adviceCache = indicator.LabIndicatorAdviceCaches
                .FirstOrDefault(c => !c.IsDeleted && c.Status == status);

            _unitOfWork.LabTestResultDetails.Add(new LabTestResultDetail
            {
                Id = Guid.NewGuid(),
                TestSessionId = sessionId,
                IndicatorId = indicator.Id,
                RawExtractedName = row.TestName,
                RawExtractedValue = row.Value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                UserValue = row.Value,
                Status = status,
                IsMatched = true,
                MatchConfidence = match.Confidence,
                ReferenceMinUsed = referenceMinUsed,
                ReferenceMaxUsed = referenceMaxUsed,
                ReferenceUnitUsed = referenceUnitUsed,
                DeviationPercent = deviationPercent,
                AdviceCacheId = adviceCache?.Id,
                CreatedAt = utcNow,
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static AgeGroup? ResolveAgeGroup(int? patientAgeAtTest)
    {
        if (patientAgeAtTest is null)
        {
            return null;
        }

        return patientAgeAtTest.Value < 18 ? AgeGroup.Child : AgeGroup.Adult;
    }
}
