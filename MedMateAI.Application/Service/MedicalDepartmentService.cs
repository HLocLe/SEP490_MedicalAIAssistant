using System.Text.Json;
using MedMateAI.Application.DTOs.MedicalDepartments.Requests;
using MedMateAI.Application.DTOs.MedicalDepartments.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class MedicalDepartmentService : IMedicalDepartmentService
{
    private const string AllDepartmentsCacheKey = "medical-departments:all";
    private const string DepartmentCacheKeyPrefix = "medical-departments:";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheTtl,
    };

    private readonly IGenericRepository<MedicalDepartment> _medicalDepartmentRepository;
    private readonly IGenericRepository<IcdChapter> _icdChapterRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedCache _cache;
    private readonly ILogger<MedicalDepartmentService> _logger;

    public MedicalDepartmentService(
        IGenericRepository<MedicalDepartment> medicalDepartmentRepository,
        IGenericRepository<IcdChapter> icdChapterRepository,
        IUnitOfWork unitOfWork,
        IDistributedCache cache,
        ILogger<MedicalDepartmentService> logger)
    {
        _medicalDepartmentRepository = medicalDepartmentRepository;
        _icdChapterRepository = icdChapterRepository;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MedicalDepartmentResponse>> ListMedicalDepartmentsAsync(
        CancellationToken cancellationToken = default)
    {

        string? cached;

        try
        {
            cached = await _cache.GetStringAsync(AllDepartmentsCacheKey, cancellationToken);
        }
        catch (Exception ex)
        {
            cached = null;
            _logger.LogWarning(ex, "Redis cache read failed for key {CacheKey}. Loading from database.", AllDepartmentsCacheKey);
        }

        if (!string.IsNullOrWhiteSpace(cached))
        {
            var cachedResponse = JsonSerializer.Deserialize<List<MedicalDepartmentResponse>>(cached);
            if (cachedResponse is not null)
            {
                return cachedResponse;
            }
        }

        var entities = await _medicalDepartmentRepository.GetAllAsync(cancellationToken);

        var response = entities
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.DepartmentName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(MapToResponse)
            .ToList();

        try
        {
            await _cache.SetStringAsync(
                AllDepartmentsCacheKey,
                JsonSerializer.Serialize(response),
                CacheOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache write failed for key {CacheKey}. Response served from database.", AllDepartmentsCacheKey);
        }

        return response;
    }

    public async Task<MedicalDepartmentResponse?> GetMedicalDepartmentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var cacheKey = GetDepartmentCacheKey(id);
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var cachedResponse = JsonSerializer.Deserialize<MedicalDepartmentResponse>(cached);
            if (cachedResponse is not null)
            {
                return cachedResponse;
            }
        }

        var entity = await _medicalDepartmentRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
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

    public async Task<(bool Succeeded, IEnumerable<string> Errors, MedicalDepartmentResponse? Data)> CreateMedicalDepartmentAsync(
        CreateMedicalDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return (false, new[] { "Request body là bắt buộc" }, null);
        }

        if (string.IsNullOrWhiteSpace(request.DepartmentName))
        {
            return (false, new[] { "DepartmentName là bắt buộc" }, null);
        }

        var chapterCode = NormalizeChapterCode(request.ChapterCode);

        if (chapterCode is not null)
        {
            var chapterExists = await _icdChapterRepository.FirstOrDefaultAsync(
                chapter => chapter.ChapterCode == chapterCode && !chapter.IsDeleted,
                cancellationToken: cancellationToken);

            if (chapterExists is null)
            {
                return (false, new[] { "Không tìm thấy ICD chapter" }, null);
            }
        }

        var entity = new MedicalDepartment
        {
            Id = Guid.NewGuid(),
            DepartmentName = request.DepartmentName.Trim(),
            Description = request.Description,
            ChapterCode = chapterCode,
            CreatedAt = DateTime.UtcNow,
        };

        _medicalDepartmentRepository.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateDepartmentCachesAsync(entity.Id, cancellationToken);

        return (true, Array.Empty<string>(), MapToResponse(entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors, MedicalDepartmentResponse? Data)> UpdateMedicalDepartmentAsync(
        Guid id,
        UpdateMedicalDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Id khoa không hợp lệ" }, null);
        }

        if (request is null)
        {
            return (false, false, new[] { "Request body là bắt buộc" }, null);
        }

        if (request.DepartmentName is not null && string.IsNullOrWhiteSpace(request.DepartmentName))
        {
            return (false, false, new[] { "DepartmentName không được để trống" }, null);
        }

        if (request.DepartmentName is null
            && request.Description is null
            && request.ChapterCode is null)
        {
            return (false, false, new[] { "Không có trường nào để cập nhật" }, null);
        }

        var entity = await _medicalDepartmentRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy khoa" }, null);
        }

        if (request.DepartmentName is not null)
        {
            entity.DepartmentName = request.DepartmentName.Trim();
        }

        if (request.Description is not null)
        {
            entity.Description = request.Description;
        }

        if (request.ChapterCode is not null)
        {
            var chapterCode = NormalizeChapterCode(request.ChapterCode);

            if (chapterCode is not null)
            {
                var chapterExists = await _icdChapterRepository.FirstOrDefaultAsync(
                    chapter => chapter.ChapterCode == chapterCode && !chapter.IsDeleted,
                    cancellationToken: cancellationToken);

                if (chapterExists is null)
                {
                    return (false, false, new[] { "Không tìm thấy ICD chapter" }, null);
                }
            }

            entity.ChapterCode = chapterCode;
        }

        entity.UpdatedAt = DateTime.UtcNow;

        _medicalDepartmentRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateDepartmentCachesAsync(id, cancellationToken);

        return (true, false, Array.Empty<string>(), MapToResponse(entity));
    }

    public async Task<(bool Succeeded, bool NotFound, IEnumerable<string> Errors)> SoftDeleteMedicalDepartmentAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, false, new[] { "Id khoa không hợp lệ" });
        }

        var entity = await _medicalDepartmentRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return (false, true, new[] { "Không tìm thấy khoa" });
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
       

        _medicalDepartmentRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateDepartmentCachesAsync(id, cancellationToken);

        return (true, false, Array.Empty<string>());
    }

    private static string? NormalizeChapterCode(string? chapterCode)
    {
        if (string.IsNullOrWhiteSpace(chapterCode))
        {
            return null;
        }

        return chapterCode.Trim().ToUpperInvariant();
    }

    private static MedicalDepartmentResponse MapToResponse(MedicalDepartment entity)
    {
        return new MedicalDepartmentResponse
        {
            Id = entity.Id,
            DepartmentName = entity.DepartmentName,
            Description = entity.Description,
            ChapterCode = entity.ChapterCode,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private async Task InvalidateDepartmentCachesAsync(Guid? id, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(AllDepartmentsCacheKey, cancellationToken);

        if (id.HasValue && id.Value != Guid.Empty)
        {
            await _cache.RemoveAsync(GetDepartmentCacheKey(id.Value), cancellationToken);
        }
    }

    private static string GetDepartmentCacheKey(Guid id)
    {
        return $"{DepartmentCacheKeyPrefix}{id}";
    }
}
