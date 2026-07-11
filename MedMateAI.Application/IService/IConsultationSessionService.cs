using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.ConsultationSessions.Responses;

namespace MedMateAI.Application.IService;

public interface IConsultationSessionService
{
    Task<(bool Succeeded, IEnumerable<string> Errors, GenerateConsultationQuestionsResponse? Data)> GenerateDoctorQuestionsAsync(
        Guid userId,
        Guid departmentId,
        string symptoms,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ConsultationSessionSummaryResponse>> GetMyCompletedSessionsAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(bool NotFound, ConsultationSessionDetailResponse? Data)> GetConsultationSessionByIdAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}