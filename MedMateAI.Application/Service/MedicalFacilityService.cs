using System.Text.Json;
using AutoMapper;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.MedicalFacilities.Requests;
using MedMateAI.Application.DTOs.MedicalFacilities.Responses;
using MedMateAI.Application.Helpers.GeoDistance;
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
    private const double MinNearbyRadiusKm = 0.1;
    private const double MaxNearbyRadiusKm = 50;
    private const int DefaultNearbyLimit = 20;
    private const int MaxNearbyLimit = 100;
    private const int DefaultTopRatedLimit = 5;
    private const int MaxTopRatedLimit = 20;

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

        var items = paged.Items.Select(facility => _mapper.Map<MedicalFacilityResponse>(facility)).ToList();
        await ApplyApprovedRatingsAsync(items, cancellationToken);

        return new PagedResponse<MedicalFacilityResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = items,
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
                    await ApplyApprovedRatingsAsync(cachedResponse, cancellationToken);
                    return cachedResponse;
                }
            }
        }

        var entities = await _unitOfWork.MedicalFacilities.GetActiveWithDepartmentsAsync(
            departmentId,
            search,
            cancellationToken);

        var response = entities.Select(facility => _mapper.Map<MedicalFacilityResponse>(facility)).ToList();
        await ApplyApprovedRatingsAsync(response, cancellationToken);

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

    public async Task<(IEnumerable<string> Errors, IReadOnlyList<MedicalFacilityNearbyResponse> Data)> ListNearbyMedicalFacilitiesAsync(
        double latitude,
        double longitude,
        double radiusKm,
        Guid? departmentId = null,
        int limit = DefaultNearbyLimit,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateNearbyQuery(latitude, longitude, radiusKm, departmentId, limit);
        if (errors.Count > 0)
        {
            return (errors, Array.Empty<MedicalFacilityNearbyResponse>());
        }

        var (minLat, maxLat, minLon, maxLon) = GeoDistanceHelper.GetBoundingBox(
            latitude,
            longitude,
            radiusKm);

        var candidates = await _unitOfWork.MedicalFacilities.GetActiveWithCoordinatesInBoundsAsync(
            minLat,
            maxLat,
            minLon,
            maxLon,
            departmentId,
            cancellationToken);

        var normalizedLimit = limit < 1 ? DefaultNearbyLimit : limit;
        normalizedLimit = normalizedLimit > MaxNearbyLimit ? MaxNearbyLimit : normalizedLimit;

        var nearby = candidates
            .Select(facility =>
            {
                var facilityLat = (double)facility.Latitude!.Value;
                var facilityLon = (double)facility.Longitude!.Value;
                var distanceKm = GeoDistanceHelper.DistanceKm(
                    latitude,
                    longitude,
                    facilityLat,
                    facilityLon);
                var mapped = _mapper.Map<MedicalFacilityNearbyResponse>(facility);
                mapped.DistanceKm = Math.Round(distanceKm, 3, MidpointRounding.AwayFromZero);
                return mapped;
            })
            .Where(facility => facility.DistanceKm <= radiusKm)
            .OrderBy(facility => facility.DistanceKm)
            .ThenBy(facility => (facility.FacilityName ?? string.Empty).ToLowerInvariant())
            .ThenBy(facility => facility.Id)
            .Take(normalizedLimit)
            .ToList();

        await ApplyApprovedRatingsAsync(nearby, cancellationToken);

        return (Array.Empty<string>(), nearby);
    }

    public async Task<(IEnumerable<string> Errors, IReadOnlyList<MedicalFacilityResponse> Data)> ListTopRatedMedicalFacilitiesAsync(
        Guid? departmentId = null,
        int limit = DefaultTopRatedLimit,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateTopRatedQuery(departmentId, limit);
        if (errors.Count > 0)
        {
            return (errors, Array.Empty<MedicalFacilityResponse>());
        }

        var normalizedLimit = limit < 1 ? DefaultTopRatedLimit : limit;
        normalizedLimit = normalizedLimit > MaxTopRatedLimit ? MaxTopRatedLimit : normalizedLimit;

        var entities = await _unitOfWork.MedicalFacilities.GetActiveWithDepartmentsAsync(
            departmentId,
            search: null,
            cancellationToken);

        var facilities = entities
            .Select(facility => _mapper.Map<MedicalFacilityResponse>(facility))
            .ToList();

        await ApplyApprovedRatingsAsync(facilities, cancellationToken);

        var topRated = facilities
            .OrderByDescending(facility => facility.AverageRating.HasValue)
            .ThenByDescending(facility => facility.AverageRating ?? 0)
            .ThenByDescending(facility => facility.ReviewCount)
            .ThenBy(facility => (facility.FacilityName ?? string.Empty).ToLowerInvariant())
            .ThenBy(facility => facility.Id)
            .Take(normalizedLimit)
            .ToList();

        return (Array.Empty<string>(), topRated);
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
                await ApplyApprovedRatingsAsync(new List<MedicalFacilityResponse> { cachedResponse }, cancellationToken);
                return cachedResponse;
            }
        }

        var entity = await _unitOfWork.MedicalFacilities.GetByIdWithDepartmentsAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var response = _mapper.Map<MedicalFacilityResponse>(entity);
        await ApplyApprovedRatingsAsync(new List<MedicalFacilityResponse> { response }, cancellationToken);
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
            return (false, new[] { "Request body là bắt buộc" }, null);
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
            validation.Errors.Add("Một số DepartmentId không tồn tại hoặc đã xóa");
            return (false, validation.Errors, null);
        }

        var normalizedAddress = NormalizeText(request.Address);
        if (await HasDuplicateFacilityAsync(null, validation.FacilityName!, normalizedAddress, cancellationToken))
        {
            return (false, new[] { "Cơ sở y tế cùng tên và địa chỉ đã tồn tại" }, null);
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
        var createdResponse = _mapper.Map<MedicalFacilityResponse>(created ?? entity);
        await ApplyApprovedRatingsAsync(new List<MedicalFacilityResponse> { createdResponse }, cancellationToken);
        return (true, Array.Empty<string>(), createdResponse);
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, MedicalFacilityResponse? Data)> UpdateMedicalFacilityAsync(
        Guid id,
        UpdateMedicalFacilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Id cơ sở y tế không hợp lệ" }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body là bắt buộc" }, null);
        }

        var entity = await _unitOfWork.MedicalFacilities.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy cơ sở y tế" }, null);
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
                errors.Add("Tên cơ sở y tế không được để trống");
            }
        }

        if (request.Latitude.HasValue && !IsLatitudeValid(request.Latitude.Value))
        {
            errors.Add("Latitude phải từ -90 đến 90");
        }

        if (request.Longitude.HasValue && !IsLongitudeValid(request.Longitude.Value))
        {
            errors.Add("Longitude phải từ -180 đến 180");
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
                errors.Add("Website không hợp lệ");
            }
        }

        if (request.ImageUrl is not null)
        {
            imageUrlFromRequest = NormalizeText(request.ImageUrl);
            if (imageUrlFromRequest is not null)
            {
                if (imageUrlFromRequest.Length > ImageUrlMaxLength)
                {
                    errors.Add("ImageUrl không được vượt quá 2048 ký tự");
                }

                if (!IsValidHttpUrl(imageUrlFromRequest))
                {
                    errors.Add("ImageUrl không hợp lệ");
                }
            }
        }

        List<Guid>? distinctDepartmentIds = null;
        if (request.DepartmentIds is not null)
        {
            var requestedDepartmentIds = request.DepartmentIds.ToList();
            if (requestedDepartmentIds.Any(x => x == Guid.Empty))
            {
                errors.Add("DepartmentIds chứa Guid rỗng");
            }

            if (requestedDepartmentIds.Count != requestedDepartmentIds.Distinct().Count())
            {
                errors.Add("DepartmentIds phải là các giá trị khác nhau");
            }

            distinctDepartmentIds = requestedDepartmentIds.Distinct().ToList();
            var invalidDepartmentIds = await GetInvalidDepartmentIdsAsync(distinctDepartmentIds, cancellationToken);
            if (invalidDepartmentIds.Count > 0)
            {
                errors.Add("Một số DepartmentId không tồn tại hoặc đã xóa");
            }
        }

        if (request.FacilityType.HasValue && !IsValidFacilityType(request.FacilityType.Value))
        {
            errors.Add("FacilityType không hợp lệ");
        }

        if (errors.Count > 0)
        {
            return (false, false, errors, null);
        }

        var finalFacilityName = facilityNameFromRequest ?? NormalizeText(entity.FacilityName);
        if (string.IsNullOrWhiteSpace(finalFacilityName))
        {
            return (false, false, new[] { "Tên cơ sở y tế là bắt buộc" }, null);
        }

        var finalAddress = request.Address is not null
            ? addressFromRequest
            : NormalizeText(entity.Address);

        if (await HasDuplicateFacilityAsync(id, finalFacilityName, finalAddress, cancellationToken))
        {
            return (false, false, new[] { "Cơ sở y tế cùng tên và địa chỉ đã tồn tại" }, null);
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
        var updatedResponse = _mapper.Map<MedicalFacilityResponse>(updated ?? entity);
        await ApplyApprovedRatingsAsync(new List<MedicalFacilityResponse> { updatedResponse }, cancellationToken);
        return (true, false, Array.Empty<string>(), updatedResponse);
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, MedicalFacilityResponse? Data)> UpdateMedicalFacilityStatusAsync(
        Guid id,
        UpdateMedicalFacilityStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Id cơ sở y tế không hợp lệ" }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body là bắt buộc" }, null);
        }

        var entity = await _unitOfWork.MedicalFacilities.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy cơ sở y tế" }, null);
        }

        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.MedicalFacilities.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateMedicalFacilityCachesAsync(id, cancellationToken);

        var updated = await _unitOfWork.MedicalFacilities.GetByIdWithDepartmentsAsync(id, cancellationToken);
        var statusResponse = _mapper.Map<MedicalFacilityResponse>(updated ?? entity);
        await ApplyApprovedRatingsAsync(new List<MedicalFacilityResponse> { statusResponse }, cancellationToken);
        return (true, false, Array.Empty<string>(), statusResponse);
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteMedicalFacilityAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Id cơ sở y tế không hợp lệ" });
        }

        var entity = await _unitOfWork.MedicalFacilities.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy cơ sở y tế" });
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
            errors.Add("Tên cơ sở y tế là bắt buộc");
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
            errors.Add("FacilityType không hợp lệ");
        }

        return (errors, facilityName, website, imageUrl, departmentIds);
    }

    private static void ValidateCoordinates(decimal? latitude, decimal? longitude, List<string> errors)
    {
        if (latitude.HasValue && !IsLatitudeValid(latitude.Value))
        {
            errors.Add("Latitude phải từ -90 đến 90");
        }

        if (longitude.HasValue && !IsLongitudeValid(longitude.Value))
        {
            errors.Add("Longitude phải từ -180 đến 180");
        }
    }

    private static void ValidateWebsite(string? website, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(website) && !IsValidAbsoluteUrl(website))
        {
            errors.Add("Website không hợp lệ");
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
            errors.Add("ImageUrl không được vượt quá 2048 ký tự");
        }

        if (!IsValidHttpUrl(imageUrl))
        {
            errors.Add("ImageUrl không hợp lệ");
        }
    }

    private static void ValidateDepartmentIds(IReadOnlyCollection<Guid> departmentIds, List<string> errors)
    {
        if (departmentIds.Any(x => x == Guid.Empty))
        {
            errors.Add("DepartmentIds chứa Guid rỗng");
        }

        if (departmentIds.Count != departmentIds.Distinct().Count())
        {
            errors.Add("DepartmentIds phải là các giá trị khác nhau");
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

    private static List<string> ValidateNearbyQuery(
        double latitude,
        double longitude,
        double radiusKm,
        Guid? departmentId,
        int limit)
    {
        var errors = new List<string>();

        if (!IsLatitudeValid((decimal)latitude))
        {
            errors.Add("Latitude phải từ -90 đến 90");
        }

        if (!IsLongitudeValid((decimal)longitude))
        {
            errors.Add("Longitude phải từ -180 đến 180");
        }

        if (radiusKm < MinNearbyRadiusKm || radiusKm > MaxNearbyRadiusKm)
        {
            errors.Add($"RadiusKm phải từ {MinNearbyRadiusKm} đến {MaxNearbyRadiusKm}");
        }

        if (departmentId.HasValue && departmentId.Value == Guid.Empty)
        {
            errors.Add("DepartmentId không hợp lệ");
        }

        if (limit < 1 || limit > MaxNearbyLimit)
        {
            errors.Add($"Limit phải từ 1 đến {MaxNearbyLimit}");
        }

        return errors;
    }

    private static List<string> ValidateTopRatedQuery(Guid? departmentId, int limit)
    {
        var errors = new List<string>();

        if (departmentId.HasValue && departmentId.Value == Guid.Empty)
        {
            errors.Add("DepartmentId không hợp lệ");
        }

        if (limit < 1 || limit > MaxTopRatedLimit)
        {
            errors.Add($"Limit phải từ 1 đến {MaxTopRatedLimit}");
        }

        return errors;
    }

    private async Task ApplyApprovedRatingsAsync<T>(
        IList<T> facilities,
        CancellationToken cancellationToken)
        where T : MedicalFacilityResponse
    {
        if (facilities.Count == 0)
        {
            return;
        }

        var facilityIds = facilities.Select(facility => facility.Id).Distinct().ToList();
        var summaries = await _unitOfWork.FeedbackReviews.GetApprovedRatingSummariesByFacilityIdsAsync(
            facilityIds,
            cancellationToken);

        foreach (var facility in facilities)
        {
            if (summaries.TryGetValue(facility.Id, out var summary))
            {
                facility.AverageRating = Math.Round(summary.AverageRating, 2, MidpointRounding.AwayFromZero);
                facility.ReviewCount = summary.ReviewCount;
            }
            else
            {
                facility.AverageRating = null;
                facility.ReviewCount = 0;
            }
        }
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
