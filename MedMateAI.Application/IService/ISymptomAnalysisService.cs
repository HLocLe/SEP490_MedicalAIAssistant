using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.SymptomAnalysis.Requests;
using MedMateAI.Application.DTOs.SymptomAnalysis.Responses.Session;
using MedMateAI.Application.DTOs.SymptomAnalysis.Responses.ClinicalQuestions;

namespace MedMateAI.Application.IService;

public interface ISymptomAnalysisService
{
    Task<SymptomAnalysisResponse?> GetSessionByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<SymptomAnalysisSessionSummaryResponse>> GetSessionsByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SuggestClinicalQuestionsResponse> SuggestClinicalQuestionAsync(
        SuggestClinicalQuestionRequest request,
        CancellationToken cancellationToken = default);

    Task<ClinicalQuestionAnswersResponse> SubmitClinicalQuestionAnswersAsync(
        SubmitClinicalQuestionAnswersRequest request,
        CancellationToken cancellationToken = default);

    Task<DiagnosisSubmitResponse> SubmitDiagnosisAsync(
        SubmitClinicalQuestionAnswersRequest request,
        CancellationToken cancellationToken = default);
}
