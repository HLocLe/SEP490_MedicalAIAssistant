using MedMateAI.Application.DTOs.AIConfigs.Requests;
using MedMateAI.Application.DTOs.AIConfigs.Responses;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;

namespace MedMateAI.Application.Service;

public sealed class AIConfigService : IAIConfigService
{
    private readonly IGenericRepository<AISystemConfig> _aiConfigRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AIConfigService(
        IGenericRepository<AISystemConfig> aiConfigRepository,
        IUnitOfWork unitOfWork)
    {
        _aiConfigRepository = aiConfigRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResponse<AIConfigResponse>> ListAIConfigsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paged = await _aiConfigRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            x => !x.IsDeleted,
            q => q.OrderBy(x => x.TaskType),
            cancellationToken: cancellationToken);

        return new PagedResponse<AIConfigResponse>
        {
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages,
            Items = paged.Items.Select(MapToResponse).ToList(),
        };
    }

    public async Task<IReadOnlyList<AIConfigResponse>> ListActiveAIConfigsAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _aiConfigRepository.GetAllAsync(cancellationToken);

        return entities
            .Where(x => !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.TaskType, StringComparer.OrdinalIgnoreCase)
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<AIConfigResponse?> GetAIConfigByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var entity = await _aiConfigRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return null;
        }

        return MapToResponse(entity);
    }

    public async Task<AIConfigResponse?> GetActiveAIConfigByTaskTypeAsync(
        string taskType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskType))
        {
            throw new ArgumentException("TaskType là bắt buộc");
        }

        var normalizedTaskType = taskType.Trim();
        var normalizedTaskTypeLower = normalizedTaskType.ToLowerInvariant();
        var entity = await _aiConfigRepository.FirstOrDefaultAsync(
            x => !x.IsDeleted && x.IsActive && x.TaskType.ToLower() == normalizedTaskTypeLower,
            asNoTracking: true,
            cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return MapToResponse(entity);
    }

    public async Task<AIConfigResponse> CreateAIConfigAsync(
        CreateAIConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentException("Request body là bắt buộc");
        }

        if (string.IsNullOrWhiteSpace(request.TaskType))
        {
            throw new ArgumentException("TaskType là bắt buộc");
        }

        if (!IsValidTemperature(request.Temperature))
        {
            throw new ArgumentException("Temperature phải từ 0 đến 2");
        }

        if (!IsValidMaxTokens(request.MaxTokens))
        {
            throw new ArgumentException("MaxTokens phải lớn hơn 0");
        }

        var taskType = request.TaskType.Trim();
        var taskTypeLower = taskType.ToLowerInvariant();

        var duplicatedConfig = await _aiConfigRepository.FirstOrDefaultAsync(
            x => !x.IsDeleted && x.TaskType.ToLower() == taskTypeLower,
            asNoTracking: true,
            cancellationToken);

        if (duplicatedConfig is not null)
        {
            throw new InvalidOperationException("TaskType đã tồn tại");
        }

        var entity = new AISystemConfig
        {
            Id = Guid.NewGuid(),
            TaskType = taskType,
            SystemPrompt = NormalizeTextAllowEmpty(request.SystemPrompt),
            Model = NormalizeTextAllowEmpty(request.Model),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        _aiConfigRepository.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(entity);
    }

    public async Task<AIConfigResponse?> UpdateAIConfigAsync(
        Guid id,
        UpdateAIConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        if (request is null)
        {
            throw new ArgumentException("Request body là bắt buộc");
        }

        var entity = await _aiConfigRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return null;
        }

        if (request.TaskType is not null)
        {
            if (string.IsNullOrWhiteSpace(request.TaskType))
            {
                throw new ArgumentException("TaskType không được để trống");
            }

            var normalizedTaskType = request.TaskType.Trim();
            if (!string.Equals(entity.TaskType, normalizedTaskType, StringComparison.OrdinalIgnoreCase))
            {
                var normalizedTaskTypeLower = normalizedTaskType.ToLowerInvariant();
                var duplicatedConfig = await _aiConfigRepository.FirstOrDefaultAsync(
                    x => !x.IsDeleted && x.Id != id && x.TaskType.ToLower() == normalizedTaskTypeLower,
                    asNoTracking: true,
                    cancellationToken);

                if (duplicatedConfig is not null)
                {
                    throw new InvalidOperationException("TaskType đã tồn tại");
                }
            }

            entity.TaskType = normalizedTaskType;
        }

        if (request.SystemPrompt is not null)
        {
            entity.SystemPrompt = NormalizeTextAllowEmpty(request.SystemPrompt);
        }

        if (request.Model is not null)
        {
            entity.Model = NormalizeTextAllowEmpty(request.Model);
        }

        if (request.Temperature.HasValue)
        {
            if (!IsValidTemperature(request.Temperature))
            {
                throw new ArgumentException("Temperature phải từ 0 đến 2");
            }

            entity.Temperature = request.Temperature;
        }

        if (request.MaxTokens.HasValue)
        {
            if (!IsValidMaxTokens(request.MaxTokens))
            {
                throw new ArgumentException("MaxTokens phải lớn hơn 0");
            }

            entity.MaxTokens = request.MaxTokens;
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        entity.UpdatedAt = DateTime.UtcNow;

        _aiConfigRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(entity);
    }

    public async Task<AIConfigResponse?> UpdateAIConfigStatusAsync(
        Guid id,
        UpdateAIConfigStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        if (request is null)
        {
            throw new ArgumentException("Request body là bắt buộc");
        }

        var entity = await _aiConfigRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return null;
        }

        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        _aiConfigRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAIConfigAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return false;
        }

        var entity = await _aiConfigRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return false;
        }

        var utcNow = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = utcNow;
        entity.UpdatedAt = utcNow;

        _aiConfigRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static AIConfigResponse MapToResponse(AISystemConfig entity)
    {
        return new AIConfigResponse
        {
            Id = entity.Id,
            TaskType = entity.TaskType,
            SystemPrompt = entity.SystemPrompt,
            Model = entity.Model,
            Temperature = entity.Temperature,
            MaxTokens = entity.MaxTokens,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private static bool IsValidTemperature(decimal? temperature)
    {
        return !temperature.HasValue || temperature.Value is >= 0 and <= 2;
    }

    private static bool IsValidMaxTokens(int? maxTokens)
    {
        return !maxTokens.HasValue || maxTokens.Value > 0;
    }

    private static string? NormalizeTextAllowEmpty(string? value)
    {
        return value?.Trim();
    }

}
