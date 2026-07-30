using System.Text.Json;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.Doctors.Requests;
using MedMateAI.Application.DTOs.Doctors.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.Extensions.Caching.Distributed;

namespace MedMateAI.Application.Service;

public sealed class DoctorService : IDoctorService
{
    private const string DoctorCacheKeyPrefix = "doctors:";
    private const int ImageUrlMaxLength = 2048;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheTtl,
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedCache _cache;
    private readonly IRecoveryPlanRealtimeNotifier _realtimeNotifier;

    public DoctorService(
        IUnitOfWork unitOfWork,
        IDistributedCache cache,
        IRecoveryPlanRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<PagedResponse<DoctorResponse>> ListDoctorsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        Guid? facilityId = null,
        Guid? departmentId = null,
        bool? isActive = null,
        DepartmentRole? departmentRole = null,
        CancellationToken cancellationToken = default)
    {
        var paged = await _unitOfWork.Doctors.GetPagedWithDetailsAsync(
            pageNumber,
            pageSize,
            search,
            facilityId,
            departmentId,
            isActive,
            departmentRole,
            cancellationToken);

        return new PagedResponse<DoctorResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.Select(MapToResponse).ToList(),
        };
    }

    public async Task<PagedResponse<DoctorResponse>> ListActiveDoctorsAsync(
        int pageNumber,
        int pageSize,
        Guid? facilityId = null,
        Guid? departmentId = null,
        string? search = null,
        DepartmentRole? departmentRole = null,
        CancellationToken cancellationToken = default)
    {
        var paged = await _unitOfWork.Doctors.GetPagedWithDetailsAsync(
            pageNumber,
            pageSize,
            search,
            facilityId,
            departmentId,
            isActive: true,
            departmentRole,
            cancellationToken);

        return new PagedResponse<DoctorResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.Select(MapToResponse).ToList(),
        };
    }

    public async Task<DoctorResponse?> GetDoctorByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var cacheKey = GetDoctorCacheKey(id);
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var cachedResponse = JsonSerializer.Deserialize<DoctorResponse>(cached);
            if (cachedResponse is not null)
            {
                return cachedResponse;
            }
        }

        var entity = await _unitOfWork.Doctors.GetByIdWithDetailsAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var response = MapToResponse(entity);
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(response),
            CacheOptions,
            cancellationToken);

        return response;
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, DoctorResponse? Data)> CreateDoctorAsync(
        CreateDoctorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (false, new[] { "Request body is required." }, null);
        }

        var errors = new List<string>();

        if (request.UserId.HasValue && request.UserId.Value == Guid.Empty)
        {
            errors.Add("UserId is invalid.");
        }

        if (request.FacilityDepartmentId == Guid.Empty)
        {
            errors.Add("FacilityDepartmentId is required.");
        }

        var fullName = NormalizeText(request.FullName);
        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors.Add("Full name is required.");
        }

        if (request.YearsOfExperience.HasValue && request.YearsOfExperience.Value < 0)
        {
            errors.Add("YearsOfExperience must be greater than or equal to 0.");
        }

        var imageUrl = NormalizeText(request.ImageUrl);
        if (imageUrl is not null)
        {
            if (imageUrl.Length > ImageUrlMaxLength)
            {
                errors.Add("ImageUrl must be 2048 characters or fewer.");
            }

            if (!IsValidHttpUrl(imageUrl))
            {
                errors.Add("ImageUrl must be a valid absolute http or https URL.");
            }
        }

        if (!IsValidDepartmentRole(request.DepartmentRole))
        {
            errors.Add("DepartmentRole is invalid.");
        }

        if (errors.Count == 0)
        {
            var isValidFacilityDepartment = await IsValidFacilityDepartmentAsync(
                request.FacilityDepartmentId,
                cancellationToken);

            if (!isValidFacilityDepartment)
            {
                errors.Add("FacilityDepartmentId is invalid, deleted, or belongs to inactive facility.");
            }
        }

        if (errors.Count == 0 && fullName is not null)
        {
            var hasDuplicate = await HasDuplicateDoctorAsync(
                excludedDoctorId: null,
                facilityDepartmentId: request.FacilityDepartmentId,
                fullName: fullName,
                cancellationToken: cancellationToken);

            if (hasDuplicate)
            {
                errors.Add("Doctor with same full name already exists in this facility department.");
            }
        }

        if (errors.Count == 0 && request.UserId.HasValue)
        {
            var existingDoctorForUser = await _unitOfWork.Doctors.FirstOrDefaultAsync(
                x =>
                    !x.IsDeleted
                    && x.UserId == request.UserId.Value,
                asNoTracking: true,
                cancellationToken: cancellationToken);

            if (existingDoctorForUser is not null)
            {
                errors.Add("This user account is already linked to a doctor profile.");
            }
        }

        if (errors.Count > 0)
        {
            return (false, errors, null);
        }

        var entity = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            FacilityDepartmentId = request.FacilityDepartmentId,
            FullName = fullName,
            Specialty = NormalizeText(request.Specialty),
            AcademicTitle = NormalizeText(request.AcademicTitle),
            ImageUrl = imageUrl,
            DepartmentRole = request.DepartmentRole,
            YearsOfExperience = request.YearsOfExperience,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        _unitOfWork.Doctors.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateDoctorCachesAsync(entity.Id, cancellationToken);

        var created = await _unitOfWork.Doctors.GetByIdWithDetailsAsync(entity.Id, cancellationToken);
        return (true, Array.Empty<string>(), MapToResponse(created ?? entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, DoctorResponse? Data)> UpdateDoctorAsync(
        Guid id,
        UpdateDoctorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid doctor id." }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body is required." }, null);
        }

        var entity = await _unitOfWork.Doctors.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Doctor not found." }, null);
        }

        var errors = new List<string>();
        string? fullNameFromRequest = null;
        string? imageUrlFromRequest = null;

        if (request.FullName is not null)
        {
            fullNameFromRequest = NormalizeText(request.FullName);
            if (string.IsNullOrWhiteSpace(fullNameFromRequest))
            {
                errors.Add("Full name cannot be empty when provided.");
            }
        }

        if (request.YearsOfExperience.HasValue && request.YearsOfExperience.Value < 0)
        {
            errors.Add("YearsOfExperience must be greater than or equal to 0.");
        }

        if (request.ImageUrl is not null)
        {
            imageUrlFromRequest = NormalizeText(request.ImageUrl);
            if (imageUrlFromRequest is not null)
            {
                if (imageUrlFromRequest.Length > ImageUrlMaxLength)
                {
                    errors.Add("ImageUrl must be 2048 characters or fewer.");
                }

                if (!IsValidHttpUrl(imageUrlFromRequest))
                {
                    errors.Add("ImageUrl must be a valid absolute http or https URL.");
                }
            }
        }

        if (request.DepartmentRole.HasValue && !IsValidDepartmentRole(request.DepartmentRole.Value))
        {
            errors.Add("DepartmentRole is invalid.");
        }

        var finalFacilityDepartmentId = entity.FacilityDepartmentId;
        if (request.FacilityDepartmentId.HasValue)
        {
            if (request.FacilityDepartmentId.Value == Guid.Empty)
            {
                errors.Add("FacilityDepartmentId is invalid.");
            }
            else
            {
                finalFacilityDepartmentId = request.FacilityDepartmentId.Value;

                var isValidFacilityDepartment = await IsValidFacilityDepartmentAsync(
                    finalFacilityDepartmentId,
                    cancellationToken);

                if (!isValidFacilityDepartment)
                {
                    errors.Add("FacilityDepartmentId is invalid, deleted, or belongs to inactive facility.");
                }
            }
        }

        var finalFullName = request.FullName is null
            ? NormalizeText(entity.FullName)
            : fullNameFromRequest;

        if (string.IsNullOrWhiteSpace(finalFullName))
        {
            errors.Add("Full name is required.");
        }

        if (errors.Count == 0 && finalFullName is not null)
        {
            var hasDuplicate = await HasDuplicateDoctorAsync(
                excludedDoctorId: id,
                facilityDepartmentId: finalFacilityDepartmentId,
                fullName: finalFullName,
                cancellationToken: cancellationToken);

            if (hasDuplicate)
            {
                errors.Add("Doctor with same full name already exists in this facility department.");
            }
        }

        if (errors.Count > 0)
        {
            return (false, false, errors, null);
        }

        if (request.FacilityDepartmentId.HasValue)
        {
            entity.FacilityDepartmentId = finalFacilityDepartmentId;
        }

        if (request.FullName is not null)
        {
            entity.FullName = fullNameFromRequest;
        }

        if (request.Specialty is not null)
        {
            entity.Specialty = NormalizeText(request.Specialty);
        }

        if (request.AcademicTitle is not null)
        {
            entity.AcademicTitle = NormalizeText(request.AcademicTitle);
        }

        if (request.ImageUrl is not null)
        {
            entity.ImageUrl = imageUrlFromRequest;
        }

        if (request.DepartmentRole.HasValue)
        {
            entity.DepartmentRole = request.DepartmentRole.Value;
        }

        if (request.YearsOfExperience.HasValue)
        {
            entity.YearsOfExperience = request.YearsOfExperience.Value;
        }

        var realtimeAccessChanged = request.IsActive.HasValue
            && entity.IsActive != request.IsActive.Value;

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        var utcNow = DateTime.UtcNow;
        entity.UpdatedAt = utcNow;

        _unitOfWork.Doctors.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (realtimeAccessChanged)
        {
            await NotifyRealtimeAccessChangedAsync(entity.UserId, utcNow);
        }

        await InvalidateDoctorCachesAsync(id, cancellationToken);

        var updated = await _unitOfWork.Doctors.GetByIdWithDetailsAsync(id, cancellationToken);
        return (true, false, Array.Empty<string>(), MapToResponse(updated ?? entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, DoctorResponse? Data)> UpdateDoctorStatusAsync(
        Guid id,
        UpdateDoctorStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid doctor id." }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body is required." }, null);
        }

        var entity = await _unitOfWork.Doctors.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Doctor not found." }, null);
        }

        var realtimeAccessChanged = entity.IsActive != request.IsActive;
        var utcNow = DateTime.UtcNow;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = utcNow;

        _unitOfWork.Doctors.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (realtimeAccessChanged)
        {
            await NotifyRealtimeAccessChangedAsync(entity.UserId, utcNow);
        }

        await InvalidateDoctorCachesAsync(id, cancellationToken);

        var updated = await _unitOfWork.Doctors.GetByIdWithDetailsAsync(id, cancellationToken);
        return (true, false, Array.Empty<string>(), MapToResponse(updated ?? entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteDoctorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid doctor id." });
        }

        var entity = await _unitOfWork.Doctors.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Doctor not found." });
        }

        var utcNow = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = utcNow;
        entity.UpdatedAt = utcNow;

        _unitOfWork.Doctors.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyRealtimeAccessChangedAsync(entity.UserId, utcNow);
        await InvalidateDoctorCachesAsync(id, cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    private async Task<bool> IsValidFacilityDepartmentAsync(
        Guid facilityDepartmentId,
        CancellationToken cancellationToken)
    {
        if (facilityDepartmentId == Guid.Empty)
        {
            return false;
        }

        var facilityDepartment = await _unitOfWork.FacilityDepartments.FirstOrDefaultAsync(
            x =>
                x.Id == facilityDepartmentId
                && !x.IsDeleted
                && !x.Facility.IsDeleted
                && x.Facility.IsActive
                && !x.Department.IsDeleted,
            asNoTracking: true,
            cancellationToken: cancellationToken);

        return facilityDepartment is not null;
    }

    private async Task<bool> HasDuplicateDoctorAsync(
        Guid? excludedDoctorId,
        Guid facilityDepartmentId,
        string fullName,
        CancellationToken cancellationToken)
    {
        var normalizedFullName = fullName.Trim().ToLowerInvariant();

        var duplicate = await _unitOfWork.Doctors.FirstOrDefaultAsync(
            x =>
                !x.IsDeleted
                && x.FacilityDepartmentId == facilityDepartmentId
                && (!excludedDoctorId.HasValue || x.Id != excludedDoctorId.Value)
                && x.FullName != null
                && x.FullName.ToLower() == normalizedFullName,
            asNoTracking: true,
            cancellationToken: cancellationToken);

        return duplicate is not null;
    }

    private static DoctorResponse MapToResponse(Doctor entity)
    {
        var facilityDepartment = entity.FacilityDepartment;
        var facility = facilityDepartment?.Facility;
        var department = facilityDepartment?.Department;

        return new DoctorResponse
        {
            Id = entity.Id,
            UserId = entity.UserId,
            FacilityDepartmentId = entity.FacilityDepartmentId,
            FacilityId = facilityDepartment?.FacilityId ?? Guid.Empty,
            FacilityName = facility?.FacilityName,
            DepartmentId = facilityDepartment?.DepartmentId ?? Guid.Empty,
            DepartmentName = department?.DepartmentName,
            FullName = entity.FullName ?? string.Empty,
            Specialty = entity.Specialty,
            AcademicTitle = entity.AcademicTitle,
            ImageUrl = entity.ImageUrl,
            DepartmentRole = entity.DepartmentRole,
            DepartmentRoleName = entity.DepartmentRole.ToString(),
            YearsOfExperience = entity.YearsOfExperience,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private Task InvalidateDoctorCachesAsync(Guid id, CancellationToken cancellationToken)
    {
        return _cache.RemoveAsync(GetDoctorCacheKey(id), cancellationToken);
    }

    private Task NotifyRealtimeAccessChangedAsync(
        Guid? doctorUserId,
        DateTime occurredAtUtc)
    {
        if (!doctorUserId.HasValue)
        {
            return Task.CompletedTask;
        }

        return _realtimeNotifier.TryNotifyDoctorRealtimeAccessChangedAsync(
            doctorUserId.Value,
            occurredAtUtc,
            CancellationToken.None);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool IsValidHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool IsValidDepartmentRole(DepartmentRole role)
    {
        return Enum.IsDefined(typeof(DepartmentRole), role);
    }

    private static string GetDoctorCacheKey(Guid id)
    {
        return $"{DoctorCacheKeyPrefix}{id}";
    }
}
