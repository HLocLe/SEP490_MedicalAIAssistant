using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.LabIndicators.Responses;
using MedMateAI.Application.DTOs.LabTests.Requests;
using MedMateAI.Application.DTOs.LabTests.Responses;
using MedMateAI.Application.Helpers;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class LabTestService : ILabTestService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".tif",
        ".tiff",
        ".pdf",
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILabTestJobScheduler _jobScheduler;
    private readonly ILabTestResultAnalyzer _resultAnalyzer;

    public LabTestService(
        IUnitOfWork unitOfWork,
        ILabTestJobScheduler jobScheduler,
        ILabTestResultAnalyzer resultAnalyzer)
    {
        _unitOfWork = unitOfWork;
        _jobScheduler = jobScheduler;
        _resultAnalyzer = resultAnalyzer;
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, LabTestUploadResponse? Data)> AnalyzeFromDocumentUrlAsync(
        Guid userId,
        LabTestAnalyzeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return (false, new[] { "User id is required." }, null);
        }

        var documentUrl = request.DocumentUrl.Trim();
        var validationErrors = ValidateDocumentUrl(documentUrl);
        if (validationErrors.Count > 0)
        {
            return (false, validationErrors, null);
        }

        if (request.PatientAgeAtTest is < 0 or > 150)
        {
            return (false, new[] { "PatientAgeAtTest is invalid." }, null);
        }

        var sessionId = Guid.NewGuid();
        var session = new LabTestSession
        {
            Id = sessionId,
            UserId = userId,
            DocumentUrl = documentUrl,
            Status = LabTestSessionStatus.Processing,
            PatientGenderAtTest = request.PatientGenderAtTest,
            PatientAgeAtTest = request.PatientAgeAtTest,
            TestDate = request.TestDate,
            CreatedAt = DateTime.UtcNow,
        };

        _unitOfWork.LabTestSessions.Add(session);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _jobScheduler.EnqueueOcr(sessionId);

        return (true, Array.Empty<string>(), MapToResponse(session));
    }

    public async Task<LabTestUploadResponse?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return null;
        }

        var session = await _unitOfWork.LabTestSessionDetails.GetByIdWithResultsAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            return null;
        }

        if (session.Status == LabTestSessionStatus.Completed
            && session.LabTestResultDetails.Count == 0
            && !string.IsNullOrWhiteSpace(session.RawOcrText))
        {
            await _resultAnalyzer.AnalyzeAndPersistAsync(sessionId, cancellationToken);
            session = await _unitOfWork.LabTestSessionDetails.GetByIdWithResultsAsync(sessionId, cancellationToken);
            if (session is null || session.UserId != userId)
            {
                return null;
            }
        }

        return MapToResponse(session);
    }

    public async Task<PagedResponse<LabTestSessionSummaryResponse>> GetSessionsByUserIdAsync(
        Guid userId,
        LabTestSessionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return new PagedResponse<LabTestSessionSummaryResponse>();
        }

        var paged = await _unitOfWork.LabTestSessionDetails.GetPagedByUserIdAsync(
            userId,
            status,
            pageNumber,
            pageSize,
            cancellationToken);

        return new PagedResponse<LabTestSessionSummaryResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items
                .Select(session => new LabTestSessionSummaryResponse
                {
                    SessionId = session.Id,
                    DocumentUrl = session.DocumentUrl,
                    Status = session.Status,
                    TestDate = session.TestDate,
                    PatientGenderAtTest = session.PatientGenderAtTest,
                    PatientAgeAtTest = session.PatientAgeAtTest,
                    FacilityName = session.FacilityName,
                    ProcessedAt = session.ProcessedAt,
                    CreatedAt = session.CreatedAt,
                })
                .ToList(),
        };
    }

    public async Task<IReadOnlyList<LabTestOcrExtractResponse>?> GetOcrExtractsBySessionIdAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return null;
        }

        var session = await _unitOfWork.LabTestSessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.IsDeleted || session.UserId != userId)
        {
            return null;
        }

        var extracts = await _unitOfWork.LabTestOcrExtracts.GetAllAsync(
            x => !x.IsDeleted && x.TestSessionId == sessionId,
            query => query.OrderBy(x => x.RowIndex).ThenBy(x => x.CreatedAt),
            cancellationToken);

        return extracts
            .Select(x => new LabTestOcrExtractResponse
            {
                OcrExtractId = x.Id,
                TestSessionId = x.TestSessionId,
                RowIndex = x.RowIndex,
                ExtractedTestName = x.ExtractedTestName,
                ExtractedValue = x.ExtractedValue,
                ExtractedUnit = x.ExtractedUnit,
                ExtractedReferenceText = x.ExtractedReferenceText,
                CreatedAt = x.CreatedAt,
            })
            .ToList();
    }

    private static LabTestUploadResponse MapToResponse(LabTestSession session)
    {
        return new LabTestUploadResponse
        {
            SessionId = session.Id,
            DocumentUrl = session.DocumentUrl ?? string.Empty,
            Status = session.Status,
            RawOcrText = session.RawOcrText ?? string.Empty,
            PatientGenderAtTest = session.PatientGenderAtTest,
            PatientAgeAtTest = session.PatientAgeAtTest,
            TestDate = session.TestDate,
            ProcessedAt = session.ProcessedAt,
            Results = session.LabTestResultDetails
                .OrderBy(x => x.CreatedAt)
                .Select(MapResultItem)
                .ToList(),
        };
    }

    private static LabTestResultItemResponse MapResultItem(LabTestResultDetail detail)
    {
        ReferenceComparisonType? comparisonType = detail.ReferenceMinUsed.HasValue && detail.ReferenceMaxUsed.HasValue
            ? ReferenceComparisonType.Between
            : detail.ReferenceMaxUsed.HasValue && !detail.ReferenceMinUsed.HasValue
                ? ReferenceComparisonType.LessThanOrEqual
                : detail.ReferenceMinUsed.HasValue && !detail.ReferenceMaxUsed.HasValue
                    ? ReferenceComparisonType.GreaterThanOrEqual
                    : null;

        return new LabTestResultItemResponse
        {
            ResultDetailId = detail.Id,
            RawExtractedName = detail.RawExtractedName ?? string.Empty,
            RawExtractedValue = detail.RawExtractedValue,
            UserValue = detail.UserValue,
            Status = detail.Status,
            IsMatched = detail.IsMatched,
            MatchConfidence = detail.MatchConfidence,
            ReferenceMinUsed = detail.ReferenceMinUsed,
            ReferenceMaxUsed = detail.ReferenceMaxUsed,
            ReferenceUnitUsed = detail.ReferenceUnitUsed,
            ComparisonTypeUsed = comparisonType,
            DeviationPercent = detail.DeviationPercent,
            Indicator = detail.Indicator is null ? null : MapIndicator(detail.Indicator),
            ReferenceRangeUsed = comparisonType.HasValue
                ? new LabIndicatorReferenceRangeResponse
                {
                    ReferenceRangeId = Guid.Empty,
                    IndicatorId = detail.IndicatorId ?? Guid.Empty,
                    ComparisonType = comparisonType.Value,
                    MinValue = detail.ReferenceMinUsed,
                    MaxValue = detail.ReferenceMaxUsed,
                    Unit = detail.ReferenceUnitUsed,
                }
                : null,
            Advice = detail.AdviceCache is null ? null : MapAdvice(detail.AdviceCache),
        };
    }

    private static LabIndicatorResponse MapIndicator(LabIndicatorMaster indicator)
    {
        return new LabIndicatorResponse
        {
            IndicatorId = indicator.Id,
            Symbol = indicator.Symbol,
            FullName = indicator.FullName,
            Unit = indicator.Unit,
            MinReference = indicator.MinReference,
            MaxReference = indicator.MaxReference,
            Description = indicator.Description,
            Category = indicator.Category,
            IsActive = indicator.IsActive,
        };
    }

    private static LabIndicatorAdviceCacheResponse MapAdvice(LabIndicatorAdviceCache advice)
    {
        return new LabIndicatorAdviceCacheResponse
        {
            CacheId = advice.Id,
            IndicatorId = advice.IndicatorId,
            Status = advice.Status,
            DisplayTitle = advice.DisplayTitle,
            Summary = advice.Summary,
            PossibleCauses = advice.PossibleCauses,
            LifestyleAdvice = advice.LifestyleAdvice,
            NutritionalAdvice = advice.NutritionalAdvice,
            UrgencyLevel = advice.UrgencyLevel,
            SeverityLevel = advice.SeverityLevel,
            WarningSigns = advice.WarningSigns,
            FollowUpSuggestion = advice.FollowUpSuggestion,
            DoctorQuestions = advice.DoctorQuestions,
        };
    }

    private static List<string> ValidateDocumentUrl(string documentUrl)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(documentUrl))
        {
            errors.Add("DocumentUrl is required.");
            return errors;
        }

        if (!Uri.TryCreate(documentUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            errors.Add("DocumentUrl must be a valid absolute http or https URL.");
            return errors;
        }

        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            errors.Add("Unsupported document URL extension. Allowed: jpg, jpeg, png, bmp, tif, tiff, pdf.");
        }

        return errors;
    }
}
