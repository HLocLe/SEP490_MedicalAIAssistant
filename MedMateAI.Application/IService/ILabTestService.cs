using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.LabTests.Requests;
using MedMateAI.Application.DTOs.LabTests.Responses;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.IService;

public interface ILabTestService
{
    Task<(bool Succeeded, IEnumerable<string> Errors, LabTestUploadResponse? Data)> AnalyzeFromDocumentUrlAsync(
        Guid userId,
        LabTestAnalyzeRequest request,
        CancellationToken cancellationToken = default);

    Task<LabTestUploadResponse?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<LabTestSessionSummaryResponse>> GetSessionsByUserIdAsync(
        Guid userId,
        LabTestSessionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
