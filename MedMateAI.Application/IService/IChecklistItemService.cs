using MedMateAI.Application.DTOs.ChecklistItems.Requests;
using MedMateAI.Application.DTOs.ChecklistItems.Responses;
using MedMateAI.Application.DTOs.Common;

namespace MedMateAI.Application.IService;

public interface IChecklistItemService
{
    Task<PagedResponse<ChecklistItemResponse>> ListAsync(
        int pageNumber,
        int pageSize,
        Guid? departmentId = null,
        Guid? facilityId = null,
        bool? isMandatory = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<ChecklistItemResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChecklistItemResponse>> GetByDepartmentIdAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChecklistItemResponse>> GetByFacilityIdAsync(
        Guid facilityId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, ChecklistItemResponse? Data)> CreateAsync(
        CreateChecklistItemRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IEnumerable<string> Errors, IReadOnlyList<ChecklistItemResponse>? Data)> BulkCreateAsync(
        BulkCreateChecklistItemsRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, ChecklistItemResponse? Data)> UpdateAsync(
        Guid id,
        UpdateChecklistItemRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
