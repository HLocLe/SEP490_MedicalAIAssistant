using System.Text.Json;
using AutoMapper;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.MedicalFacilities.Requests;
using MedMateAI.Application.DTOs.MedicalFacilities.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using Microsoft.Extensions.Caching.Distributed;

namespace MedMateAI.Application.Service;

public sealed class MedicalFacilityService : IMedicalFacilityService
{
    private const string ActiveFacilitiesCacheKey = "medical-facilities:active";
    private const string FacilityCacheKeyPrefix = "medical-facilities:";
    private const int ImageUrlMaxLength = 2048;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheTtl,
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedCache _cache;
    private readonly IMapper _mapper;

    public MedicalFacilityService(
        IUnitOfWork unitOfWork,
        IDistributedCache cache,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<PagedResponse<MedicalFacilityResponse>> ListMedicalFacilitiesAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var paged = await _unitOfWork.MedicalFacilities.GetPagedWithDepartmentsAsync(
            pageNumber,
            pageSize,
            search,
            isActive,
            cancellationToken);

        return new PagedResponse<MedicalFacilityResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.Select(facility => _mapper.Map<MedicalFacilityResponse>(facility)).ToList(),
        };
    }

    public async Task<IReadOnlyList<MedicalFacilityResponse>> ListActiveMedicalFacilitiesAsync(
        Guid? departmentId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var shouldUseCache = !departmentId.HasValue && string.IsNullOrWhiteSpace(search);
        if (shouldUseCache)
        {
            var cached = await _cache.GetStringAsync(ActiveFacilitiesCacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                var cachedResponse = JsonSerializer.Deserialize<List<MedicalFacilityResponse>>(cached);
                if (cachedResponse is not null)
                {
                    return cachedResponse;
                }
            }
        }

        var entities = await _unitOfWork.MedicalFacilities.GetActiveWithDepartmentsAsync(
            departmentId,
            search,
            cancellationToken);

        var response = entities.Select(facility => _mapper.Map<MedicalFacilityResponse>(facility)).ToList();

        if (shouldUseCache)
        {
            await _cache.SetStringAsync(
                ActiveFacilitiesCacheKey,
                JsonSerializer.Serialize(response),
                CacheOptions,
                cancellationToken);
        }

        return response;
    }

    public async Task<MedicalFacilityResponse?> GetMedicalFacilityByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var cacheKey = GetFacilityCacheKey(id);
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var cachedResponse = JsonSerializer.Deserialize<MedicalFacilityResponse>(cached);
            if (cachedResponse is not null)
            {
                return cachedResponse;
            }
        }

        var entity = await _unitOfWork.MedicalFacilities.GetByIdWithDepartmentsAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var response = _mapper.Map<MedicalFacilityResponse>(entity);
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(response),
            CacheOptions,
            cancellationToken);

        return response;
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors, MedicalFacilityResponse? Data)> CreateMedicalFacilityAsync(
        CreateMedicalFacilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (false, new[] { "Request body is required." }, null);
        }

        var validation = ValidateCreateMedicalFacilityRequest(request);
        if (validation.Errors.Count > 0)
        {
            return (false, validation.Errors, null);
        }

        var distinctDepartmentIds = validation.DepartmentIds.Distinct().ToList();
        var invalidDepartmentIds = await GetInvalidDepartmentIdsAsync(distinctDepartmentIds, cancellationToken);
        if (invalidDepartmentIds.Count > 0)
        {
            validation.Errors.Add("Some DepartmentIds do not exist or are deleted.");
            return (false, validation.Errors, null);
        }

        var normalizedAddress = NormalizeText(request.Address);
        if (await HasDuplicateFacilityAsync(null, validation.FacilityName!, normalizedAddress, cancellationToken))
        {
            return (false, new[] { "Medical facility with same facility name and address already exists." }, null);
        }

        var utcNow = DateTime.UtcNow;
        var entity = new MedicalFacility
        {
            Id = Guid.NewGuid(),
            FacilityName = validation.FacilityName,
            Address = normalizedAddress,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Phone = NormalizeText(request.Phone),
            Website = validation.Website,
            ImageUrl = validation.ImageUrl,
            OpeningHours = NormalizeText(request.OpeningHours),
            FacilityType = request.FacilityType,
            IsActive = request.IsActive,
            CreatedAt = utcNow,
        };

        _unitOfWork.MedicalFacilities.Add(entity);

        foreach (var departmentId in distinctDepartmentIds)
        {
            _unitOfWork.FacilityDepartments.Add(new FacilityDepartment
            {
                Id = Guid.NewGuid(),
                FacilityId = entity.Id,
                DepartmentId = departmentId,
                CreatedAt = utcNow,
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateMedicalFacilityCachesAsync(entity.Id, cancellationToken);

        var created = await _unitOfWork.MedicalFacilities.GetByIdWithDepartmentsAsync(entity.Id, cancellationToken);
        return (true, Array.Empty<string>(), _mapper.Map<MedicalFacilityResponse>(created ?? entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, MedicalFacilityResponse? Data)> UpdateMedicalFacilityAsync(
        Guid id,
        UpdateMedicalFacilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid medical facility id." }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body is required." }, null);
        }

        var entity = await _unitOfWork.MedicalFacilities.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Medical facility not found." }, null);
        }

        var errors = new List<string>();
        string? facilityNameFromRequest = null;
        string? addressFromRequest = null;
        string? websiteFromRequest = null;
        string? imageUrlFromRequest = null;

        if (request.FacilityName is not null)
        {
            facilityNameFromRequest = NormalizeText(request.FacilityName);
            if (string.IsNullOrWhiteSpace(facilityNameFromRequest))
            {
                errors.Add("Facility name cannot be empty when provided.");
            }
        }

        if (request.Latitude.HasValue && !IsLatitudeValid(request.Latitude.Value))
        {
            errors.Add("Latitude must be between -90 and 90.");
        }

        if (request.Longitude.HasValue && !IsLongitudeValid(request.Longitude.Value))
        {
            errors.Add("Longitude must be between -180 and 180.");
        }

        if (request.Address is not null)
        {
            addressFromRequest = NormalizeText(request.Address);
        }

        if (request.Website is not null)
        {
            websiteFromRequest = NormalizeText(request.Website);
            if (!string.IsNullOrWhiteSpace(websiteFromRequest) && !IsValidAbsoluteUrl(websiteFromRequest))
            {
                errors.Add("Website must be a valid absolute URL.");
            }
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

        List<Guid>? distinctDepartmentIds = null;
        if (request.DepartmentIds is not null)
        {
            var requestedDepartmentIds = request.DepartmentIds.ToList();
            if (requestedDepartmentIds.Any(x => x == Guid.Empty))
            {
                errors.Add("DepartmentIds contains invalid empty guid.");
            }

            if (requestedDepartmentIds.Count != requestedDepartmentIds.Distinct().Count())
            {
                errors.Add("DepartmentIds must contain distinct values.");
            }

            distinctDepartmentIds = requestedDepartmentIds.Distinct().ToList();
            var invalidDepartmentIds = await GetInvalidDepartmentIdsAsync(distinctDepartmentIds, cancellationToken);
            if (invalidDepartmentIds.Count > 0)
            {
                errors.Add("Some DepartmentIds do not exist or are deleted.");
            }
        }

        if (request.FacilityType.HasValue && !IsValidFacilityType(request.FacilityType.Value))
        {
            errors.Add("FacilityType is invalid.");
        }

        if (errors.Count > 0)
        {
            return (false, false, errors, null);
        }

        var finalFacilityName = facilityNameFromRequest ?? NormalizeText(entity.FacilityName);
        if (string.IsNullOrWhiteSpace(finalFacilityName))
        {
            return (false, false, new[] { "Facility name is required." }, null);
        }

        var finalAddress = request.Address is not null
            ? addressFromRequest
            : NormalizeText(entity.Address);

        if (await HasDuplicateFacilityAsync(id, finalFacilityName, finalAddress, cancellationToken))
        {
            return (false, false, new[] { "Medical facility with same facility name and address already exists." }, null);
        }

        if (facilityNameFromRequest is not null)
        {
            entity.FacilityName = facilityNameFromRequest;
        }

        if (request.Address is not null)
        {
            entity.Address = addressFromRequest;
        }

        if (request.Latitude.HasValue)
        {
            entity.Latitude = request.Latitude.Value;
        }

        if (request.Longitude.HasValue)
        {
            entity.Longitude = request.Longitude.Value;
        }

        if (request.Phone is not null)
        {
            entity.Phone = NormalizeText(request.Phone);
        }

        if (request.Website is not null)
        {
            entity.Website = websiteFromRequest;
        }

        if (request.ImageUrl is not null)
        {
            entity.ImageUrl = imageUrlFromRequest;
        }

        if (request.OpeningHours is not null)
        {
            entity.OpeningHours = NormalizeText(request.OpeningHours);
        }

        if (request.FacilityType.HasValue)
        {
            entity.FacilityType = request.FacilityType.Value;
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        var utcNow = DateTime.UtcNow;
        entity.UpdatedAt = utcNow;
        _unitOfWork.MedicalFacilities.Update(entity);

        if (distinctDepartmentIds is not null)
        {
            await ReplaceFacilityDepartmentsAsync(id, distinctDepartmentIds, utcNow, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateMedicalFacilityCachesAsync(id, cancellationToken);

        var updated = await _unitOfWork.MedicalFacilities.GetByIdWithDepartmentsAsync(id, cancellationToken);
        return (true, false, Array.Empty<string>(), _mapper.Map<MedicalFacilityResponse>(updated ?? entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, MedicalFacilityResponse? Data)> UpdateMedicalFacilityStatusAsync(
        Guid id,
        UpdateMedicalFacilityStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid medical facility id." }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body is required." }, null);
        }

        var entity = await _unitOfWork.MedicalFacilities.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Medical facility not found." }, null);
        }

        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.MedicalFacilities.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateMedicalFacilityCachesAsync(id, cancellationToken);

        var updated = await _unitOfWork.MedicalFacilities.GetByIdWithDepartmentsAsync(id, cancellationToken);
        return (true, false, Array.Empty<string>(), _mapper.Map<MedicalFacilityResponse>(updated ?? entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteMedicalFacilityAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Invalid medical facility id." });
        }

        var entity = await _unitOfWork.MedicalFacilities.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Medical facility not found." });
        }

        var utcNow = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = utcNow;
        entity.UpdatedAt = utcNow;
        _unitOfWork.MedicalFacilities.Update(entity);

        var facilityDepartments = await _unitOfWork.MedicalFacilities.GetFacilityDepartmentsAsync(id, cancellationToken);
        foreach (var facilityDepartment in facilityDepartments.Where(x => !x.IsDeleted))
        {
            facilityDepartment.IsDeleted = true;
            facilityDepartment.DeletedAt = utcNow;
            facilityDepartment.UpdatedAt = utcNow;
            _unitOfWork.FacilityDepartments.Update(facilityDepartment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateMedicalFacilityCachesAsync(id, cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    private async Task ReplaceFacilityDepartmentsAsync(
        Guid facilityId,
        IReadOnlyList<Guid> departmentIds,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var currentFacilityDepartments = await _unitOfWork.MedicalFacilities.GetFacilityDepartmentsAsync(
            facilityId,
            cancellationToken);

        var requestedDepartmentIds = departmentIds.ToHashSet();
        var currentByDepartmentId = currentFacilityDepartments
            .GroupBy(x => x.DepartmentId)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var current in currentByDepartmentId.Values)
        {
            if (requestedDepartmentIds.Contains(current.DepartmentId))
            {
                if (current.IsDeleted)
                {
                    current.IsDeleted = false;
                    current.DeletedAt = null;
                    current.UpdatedAt = utcNow;
                    _unitOfWork.FacilityDepartments.Update(current);
                }

                requestedDepartmentIds.Remove(current.DepartmentId);
                continue;
            }

            if (!current.IsDeleted)
            {
                current.IsDeleted = true;
                current.DeletedAt = utcNow;
                current.UpdatedAt = utcNow;
                _unitOfWork.FacilityDepartments.Update(current);
            }
        }

        foreach (var departmentId in requestedDepartmentIds)
        {
            _unitOfWork.FacilityDepartments.Add(new FacilityDepartment
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                DepartmentId = departmentId,
                CreatedAt = utcNow,
            });
        }
    }

    private async Task<List<Guid>> GetInvalidDepartmentIdsAsync(
        IReadOnlyList<Guid> departmentIds,
        CancellationToken cancellationToken)
    {
        if (departmentIds.Count == 0)
        {
            return new List<Guid>();
        }

        var allDepartments = await _unitOfWork.MedicalDepartments.GetAllAsync(cancellationToken);
        var activeDepartmentIds = allDepartments
            .Where(x => !x.IsDeleted)
            .Select(x => x.Id)
            .ToHashSet();

        return departmentIds
            .Where(x => !activeDepartmentIds.Contains(x))
            .ToList();
    }

    private async Task<bool> HasDuplicateFacilityAsync(
        Guid? excludedFacilityId,
        string facilityName,
        string? address,
        CancellationToken cancellationToken)
    {
        var normalizedFacilityName = facilityName.Trim().ToLowerInvariant();
        var normalizedAddress = NormalizeText(address);
        var normalizedAddressLower = normalizedAddress?.ToLowerInvariant();

        var duplicated = await _unitOfWork.MedicalFacilities.FirstOrDefaultAsync(
            x => !x.IsDeleted
                 && (!excludedFacilityId.HasValue || x.Id != excludedFacilityId.Value)
                 && x.FacilityName != null
                 && x.FacilityName.ToLower() == normalizedFacilityName
                 && (
                     (normalizedAddressLower == null && string.IsNullOrEmpty(x.Address))
                     || (normalizedAddressLower != null
                         && x.Address != null
                         && x.Address.ToLower() == normalizedAddressLower)),
            asNoTracking: true,
            cancellationToken: cancellationToken);

        return duplicated is not null;
    }

    private async Task InvalidateMedicalFacilityCachesAsync(Guid id, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(ActiveFacilitiesCacheKey, cancellationToken);
        await _cache.RemoveAsync(GetFacilityCacheKey(id), cancellationToken);
    }

    private static (
        List<string> Errors,
        string? FacilityName,
        string? Website,
        string? ImageUrl,
        List<Guid> DepartmentIds) ValidateCreateMedicalFacilityRequest(CreateMedicalFacilityRequest request)
    {
        var errors = new List<string>();
        var facilityName = NormalizeText(request.FacilityName);
        if (string.IsNullOrWhiteSpace(facilityName))
        {
            errors.Add("Facility name is required.");
        }

        ValidateCoordinates(request.Latitude, request.Longitude, errors);

        var website = NormalizeText(request.Website);
        ValidateWebsite(website, errors);

        var imageUrl = NormalizeText(request.ImageUrl);
        ValidateImageUrl(imageUrl, errors);

        var departmentIds = request.DepartmentIds?.ToList() ?? new List<Guid>();
        ValidateDepartmentIds(departmentIds, errors);

        if (!IsValidFacilityType(request.FacilityType))
        {
            errors.Add("FacilityType is invalid.");
        }

        return (errors, facilityName, website, imageUrl, departmentIds);
    }

    private static void ValidateCoordinates(decimal? latitude, decimal? longitude, List<string> errors)
    {
        if (latitude.HasValue && !IsLatitudeValid(latitude.Value))
        {
            errors.Add("Latitude must be between -90 and 90.");
        }

        if (longitude.HasValue && !IsLongitudeValid(longitude.Value))
        {
            errors.Add("Longitude must be between -180 and 180.");
        }
    }

    private static void ValidateWebsite(string? website, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(website) && !IsValidAbsoluteUrl(website))
        {
            errors.Add("Website must be a valid absolute URL.");
        }
    }

    private static void ValidateImageUrl(string? imageUrl, List<string> errors)
    {
        if (imageUrl is null)
        {
            return;
        }

        if (imageUrl.Length > ImageUrlMaxLength)
        {
            errors.Add("ImageUrl must be 2048 characters or fewer.");
        }

        if (!IsValidHttpUrl(imageUrl))
        {
            errors.Add("ImageUrl must be a valid absolute http or https URL.");
        }
    }

    private static void ValidateDepartmentIds(IReadOnlyCollection<Guid> departmentIds, List<string> errors)
    {
        if (departmentIds.Any(x => x == Guid.Empty))
        {
            errors.Add("DepartmentIds contains invalid empty guid.");
        }

        if (departmentIds.Count != departmentIds.Distinct().Count())
        {
            errors.Add("DepartmentIds must contain distinct values.");
        }
    }

    private static bool IsLatitudeValid(decimal latitude)
    {
        return latitude >= -90m && latitude <= 90m;
    }

    private static bool IsLongitudeValid(decimal longitude)
    {
        return longitude >= -180m && longitude <= 180m;
    }

    private static bool IsValidAbsoluteUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }

    private static bool IsValidHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool IsValidFacilityType(MedicalFacilityType facilityType)
    {
        return Enum.IsDefined(typeof(MedicalFacilityType), facilityType);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string GetFacilityCacheKey(Guid id)
    {
        return $"{FacilityCacheKeyPrefix}{id}";
    }
}
