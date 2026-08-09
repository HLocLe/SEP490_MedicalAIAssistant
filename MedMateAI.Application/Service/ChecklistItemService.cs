using AutoMapper;
using MedMateAI.Application.DTOs.ChecklistItems.Requests;
using MedMateAI.Application.DTOs.ChecklistItems.Responses;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class ChecklistItemService : IChecklistItemService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ChecklistItemService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResponse<ChecklistItemResponse>> ListAsync(
        int pageNumber,
        int pageSize,
        Guid? departmentId = null,
        Guid? facilityId = null,
        bool? isMandatory = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var departmentIdFilter = departmentId.HasValue && departmentId.Value != Guid.Empty
            ? departmentId.Value
            : (Guid?)null;
        var facilityIdFilter = facilityId.HasValue && facilityId.Value != Guid.Empty
            ? facilityId.Value
            : (Guid?)null;
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

        var paged = await _unitOfWork.ChecklistItems.GetPagedAsync(
            pageNumber,
            pageSize,
            item => !item.IsDeleted
                && (!departmentIdFilter.HasValue || item.DepartmentId == departmentIdFilter.Value)
                && (!facilityIdFilter.HasValue || item.FacilityId == facilityIdFilter.Value)
                && (!isMandatory.HasValue || item.IsMandatory == isMandatory.Value)
                && (searchTerm == null || item.Content.ToLower().Contains(searchTerm)),
            query => query
                .OrderByDescending(item => item.IsMandatory)
                .ThenBy(item => item.Content),
            cancellationToken: cancellationToken);

        return PagedResponse<ChecklistItemResponse>.From(
            paged,
            item => _mapper.Map<ChecklistItemResponse>(item));
    }

    public async Task<ChecklistItemResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var item = await _unitOfWork.ChecklistItems.GetByIdAsync(id, cancellationToken);
        if (item is null || item.IsDeleted)
        {
            return null;
        }

        return _mapper.Map<ChecklistItemResponse>(item);
    }

    public async Task<IReadOnlyList<ChecklistItemResponse>> GetByDepartmentIdAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId == Guid.Empty)
        {
            return Array.Empty<ChecklistItemResponse>();
        }

        var items = await _unitOfWork.ChecklistItems.GetAllAsync(
            item => !item.IsDeleted && item.DepartmentId == departmentId,
            query => query
                .OrderByDescending(item => item.IsMandatory)
                .ThenBy(item => item.Content),
            cancellationToken: cancellationToken);

        return items.Select(item => _mapper.Map<ChecklistItemResponse>(item)).ToList();
    }

    public async Task<IReadOnlyList<ChecklistItemResponse>> GetByFacilityIdAsync(
        Guid facilityId,
        CancellationToken cancellationToken = default)
    {
        if (facilityId == Guid.Empty)
        {
            return Array.Empty<ChecklistItemResponse>();
        }

        var items = await _unitOfWork.ChecklistItems.GetAllAsync(
            item => !item.IsDeleted && item.FacilityId == facilityId,
            query => query
                .OrderByDescending(item => item.IsMandatory)
                .ThenBy(item => item.Content),
            cancellationToken: cancellationToken);

        return items.Select(item => _mapper.Map<ChecklistItemResponse>(item)).ToList();
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, ChecklistItemResponse? Data)> CreateAsync(
        CreateChecklistItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (false, new[] { "Request body là bắt buộc" }, null);
        }

        var validationErrors = await ValidateFieldsAsync(
            request.Content,
            request.DepartmentId,
            request.FacilityId,
            requireContent: true,
            cancellationToken);

        if (validationErrors.Count > 0)
        {
            return (false, validationErrors, null);
        }

        var entity = new ChecklistItem
        {
            Content = request.Content.Trim(),
            DepartmentId = NormalizeOptionalId(request.DepartmentId),
            FacilityId = NormalizeOptionalId(request.FacilityId),
            IsMandatory = request.IsMandatory,
        };

        _unitOfWork.ChecklistItems.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>(), _mapper.Map<ChecklistItemResponse>(entity));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, IReadOnlyList<ChecklistItemResponse>? Data)> BulkCreateAsync(
        BulkCreateChecklistItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return (false, new[] { "Cần ít nhất một mục checklist" }, null);
        }

        var errors = new List<string>();
        var prepared = new List<ChecklistItem>();
        var seenInRequest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            if (item is null)
            {
                errors.Add($"Items[{index}]: Mục checklist là bắt buộc");
                continue;
            }

            var fieldErrors = await ValidateFieldsAsync(
                item.Content,
                item.DepartmentId,
                item.FacilityId,
                requireContent: true,
                cancellationToken);

            foreach (var fieldError in fieldErrors)
            {
                errors.Add($"Items[{index}]: {fieldError}");
            }

            if (fieldErrors.Count > 0)
            {
                continue;
            }

            var content = item.Content.Trim();
            var departmentId = NormalizeOptionalId(item.DepartmentId);
            var facilityId = NormalizeOptionalId(item.FacilityId);
            var dedupeKey = $"{content}|{departmentId:D}|{facilityId:D}";

            if (!seenInRequest.Add(dedupeKey))
            {
                errors.Add($"Items[{index}]: Trùng nội dung trong request (cùng khoa/cơ sở)");
                continue;
            }

            prepared.Add(new ChecklistItem
            {
                Content = content,
                DepartmentId = departmentId,
                FacilityId = facilityId,
                IsMandatory = item.IsMandatory,
            });
        }

        if (errors.Count > 0)
        {
            return (false, errors, null);
        }

        foreach (var entity in prepared)
        {
            _unitOfWork.ChecklistItems.Add(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (
            true,
            Array.Empty<string>(),
            prepared.Select(entity => _mapper.Map<ChecklistItemResponse>(entity)).ToList());
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, ChecklistItemResponse? Data)> UpdateAsync(
        Guid id,
        UpdateChecklistItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, true, Array.Empty<string>(), null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body là bắt buộc" }, null);
        }

        var entity = await _unitOfWork.ChecklistItems.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, Array.Empty<string>(), null);
        }

        var nextContent = request.Content is null ? entity.Content : request.Content.Trim();
        var nextDepartmentId = request.DepartmentId.HasValue
            ? NormalizeOptionalId(request.DepartmentId)
            : entity.DepartmentId;
        var nextFacilityId = request.FacilityId.HasValue
            ? NormalizeOptionalId(request.FacilityId)
            : entity.FacilityId;

        var validationErrors = await ValidateFieldsAsync(
            nextContent,
            nextDepartmentId,
            nextFacilityId,
            requireContent: true,
            cancellationToken);

        if (validationErrors.Count > 0)
        {
            return (false, false, validationErrors, null);
        }

        entity.Content = nextContent;
        entity.DepartmentId = nextDepartmentId;
        entity.FacilityId = nextFacilityId;
        if (request.IsMandatory.HasValue)
        {
            entity.IsMandatory = request.IsMandatory.Value;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.ChecklistItems.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>(), _mapper.Map<ChecklistItemResponse>(entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, true, Array.Empty<string>());
        }

        var entity = await _unitOfWork.ChecklistItems.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, Array.Empty<string>());
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.ChecklistItems.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    private async Task<List<string>> ValidateFieldsAsync(
        string? content,
        Guid? departmentId,
        Guid? facilityId,
        bool requireContent,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (requireContent && string.IsNullOrWhiteSpace(content))
        {
            errors.Add("Nội dung mục checklist là bắt buộc");
        }
        else if (!string.IsNullOrWhiteSpace(content) && content.Trim().Length > 1000)
        {
            errors.Add("Nội dung mục checklist không được vượt quá 1000 ký tự");
        }

        var normalizedDepartmentId = NormalizeOptionalId(departmentId);
        if (normalizedDepartmentId.HasValue)
        {
            var department = await _unitOfWork.MedicalDepartments.GetByIdAsync(
                normalizedDepartmentId.Value,
                cancellationToken);
            if (department is null || department.IsDeleted)
            {
                errors.Add("Không tìm thấy khoa");
            }
        }

        var normalizedFacilityId = NormalizeOptionalId(facilityId);
        if (normalizedFacilityId.HasValue)
        {
            var facility = await _unitOfWork.MedicalFacilities.GetByIdAsync(
                normalizedFacilityId.Value,
                cancellationToken);
            if (facility is null || facility.IsDeleted)
            {
                errors.Add("Không tìm thấy cơ sở y tế");
            }
        }

        return errors;
    }

    private static Guid? NormalizeOptionalId(Guid? id)
        => id.HasValue && id.Value != Guid.Empty ? id.Value : null;
}
