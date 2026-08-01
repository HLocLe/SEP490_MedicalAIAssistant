using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.LabIndicators.Requests;
using MedMateAI.Application.DTOs.LabIndicators.Responses;

namespace MedMateAI.Application.IService;

public interface ILabIndicatorService
{
    Task<PagedResponse<LabIndicatorResponse>> ListLabIndicatorsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<LabIndicatorDetailResponse?> GetLabIndicatorByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, LabIndicatorResponse? Data)> CreateLabIndicatorAsync(
        CreateLabIndicatorRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorResponse>? Data)> BulkCreateLabIndicatorsAsync(
        BulkCreateLabIndicatorsRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorResponse? Data)> UpdateLabIndicatorAsync(
        Guid id,
        UpdateLabIndicatorRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteLabIndicatorAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorAliasResponse>? Data)> GetAliasesByIndicatorIdAsync(
        Guid indicatorId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorReferenceRangeResponse>? Data)> GetReferenceRangesByIndicatorIdAsync(
        Guid indicatorId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorAdviceCacheResponse>? Data)> GetAdviceCachesByIndicatorIdAsync(
        Guid indicatorId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorAliasResponse>? Data)> BulkCreateAliasesAsync(
        Guid indicatorId,
        BulkCreateLabIndicatorAliasesRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorAliasResponse? Data)> CreateAliasAsync(
        Guid indicatorId,
        CreateLabIndicatorAliasRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorAliasResponse? Data)> UpdateAliasAsync(
        Guid indicatorId,
        Guid aliasId,
        UpdateLabIndicatorAliasRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteAliasAsync(
        Guid indicatorId,
        Guid aliasId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorReferenceRangeResponse>? Data)> BulkCreateReferenceRangesAsync(
        Guid indicatorId,
        BulkCreateLabIndicatorReferenceRangesRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorReferenceRangeResponse? Data)> CreateReferenceRangeAsync(
        Guid indicatorId,
        CreateLabIndicatorReferenceRangeRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorReferenceRangeResponse? Data)> UpdateReferenceRangeAsync(
        Guid indicatorId,
        Guid referenceRangeId,
        UpdateLabIndicatorReferenceRangeRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteReferenceRangeAsync(
        Guid indicatorId,
        Guid referenceRangeId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, IReadOnlyList<LabIndicatorAdviceCacheResponse>? Data)> BulkCreateAdviceCachesAsync(
        Guid indicatorId,
        BulkCreateLabIndicatorAdviceCachesRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorAdviceCacheResponse? Data)> CreateAdviceCacheAsync(
        Guid indicatorId,
        CreateLabIndicatorAdviceCacheRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, LabIndicatorAdviceCacheResponse? Data)> UpdateAdviceCacheAsync(
        Guid indicatorId,
        Guid cacheId,
        UpdateLabIndicatorAdviceCacheRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteAdviceCacheAsync(
        Guid indicatorId,
        Guid cacheId,
        CancellationToken cancellationToken = default);
}
