using System.Text.Json;
using MedMateAI.Application.Common;
using MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Responses;
using MedMateAI.Application.DTOs.SubscriptionPlans.Requests;
using MedMateAI.Application.DTOs.SubscriptionPlans.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Caching.Distributed;

namespace MedMateAI.Application.Service;

public sealed class SubscriptionPlanService : ISubscriptionPlanService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheTtl,
    };

    private readonly IGenericRepository<SubscriptionPlan> _subscriptionPlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedCache _cache;
    private readonly ISubscriptionPlanQuotaService _quotaService;
    private readonly ISubscriptionPlanCacheInvalidator _cacheInvalidator;

    public SubscriptionPlanService(
        IGenericRepository<SubscriptionPlan> subscriptionPlanRepository,
        IUnitOfWork unitOfWork,
        IDistributedCache cache,
        ISubscriptionPlanQuotaService quotaService,
        ISubscriptionPlanCacheInvalidator cacheInvalidator)
    {
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _quotaService = quotaService;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<IReadOnlyList<SubscriptionPlanResponse>> ListSubscriptionPlansAsync(
        CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetStringAsync(
            SubscriptionPlanCacheKeys.All,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var cachedResponse = JsonSerializer.Deserialize<List<SubscriptionPlanResponse>>(cached);
            if (cachedResponse is not null)
            {
                return cachedResponse;
            }
        }

        var entities = await _subscriptionPlanRepository.GetAllAsync(cancellationToken);

        var plans = entities
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Price)
            .ThenBy(x => x.PlanName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var quotasByPlan = await _quotaService.GetActivePlanQuotasAsync(
            plans.Select(plan => plan.Id).ToArray(),
            cancellationToken);
        var response = plans
            .Select(plan => MapToResponse(plan, GetQuotas(quotasByPlan, plan.Id)))
            .ToList();

        await _cache.SetStringAsync(
            SubscriptionPlanCacheKeys.All,
            JsonSerializer.Serialize(response),
            CacheOptions,
            cancellationToken);

        return response;
    }

    public async Task<IReadOnlyList<SubscriptionPlanResponse>> ListActiveSubscriptionPlansAsync(
        CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetStringAsync(
            SubscriptionPlanCacheKeys.Active,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var cachedResponse = JsonSerializer.Deserialize<List<SubscriptionPlanResponse>>(cached);
            if (cachedResponse is not null)
            {
                return cachedResponse;
            }
        }

        var entities = await _subscriptionPlanRepository.GetAllAsync(cancellationToken);

        var plans = entities
            .Where(x => !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.Price)
            .ThenBy(x => x.PlanName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var quotasByPlan = await _quotaService.GetActivePlanQuotasAsync(
            plans.Select(plan => plan.Id).ToArray(),
            cancellationToken);
        var response = plans
            .Select(plan => MapToResponse(plan, GetQuotas(quotasByPlan, plan.Id)))
            .ToList();

        await _cache.SetStringAsync(
            SubscriptionPlanCacheKeys.Active,
            JsonSerializer.Serialize(response),
            CacheOptions,
            cancellationToken);

        return response;
    }

    public async Task<SubscriptionPlanResponse?> GetSubscriptionPlanByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var cacheKey = SubscriptionPlanCacheKeys.ForPlan(id);
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var cachedResponse = JsonSerializer.Deserialize<SubscriptionPlanResponse>(cached);
            if (cachedResponse is not null)
            {
                return cachedResponse;
            }
        }

        var entity = await _subscriptionPlanRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return null;
        }

        var quotasByPlan = await _quotaService.GetActivePlanQuotasAsync(
            new[] { entity.Id },
            cancellationToken);
        var response = MapToResponse(entity, GetQuotas(quotasByPlan, entity.Id));

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(response),
            CacheOptions,
            cancellationToken);

        return response;
    }

    public async Task<SubscriptionPlanResponse> CreateSubscriptionPlanAsync(
        CreateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PlanName))
        {
            throw new ArgumentException("Plan name is required.");
        }

        if (request.Price < 0)
        {
            throw new ArgumentException("Price must be greater than or equal to 0.");
        }

        if (request.DurationInDays <= 0)
        {
            throw new ArgumentException("DurationInDays must be greater than 0.");
        }

        if (!IsValidJson(request.FeatureLimitJson))
        {
            throw new ArgumentException("FeatureLimitJson must be valid JSON.");
        }

        var planName = request.PlanName.Trim();
        var planNameLower = planName.ToLowerInvariant();

        var duplicatedPlan = await _subscriptionPlanRepository.FirstOrDefaultAsync(
            x => !x.IsDeleted && x.PlanName != null && x.PlanName.ToLower() == planNameLower,
            asNoTracking: true,
            cancellationToken);

        if (duplicatedPlan is not null)
        {
            throw new InvalidOperationException("Plan name already exists.");
        }

        var entity = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            PlanName = planName,
            Price = request.Price,
            DurationInDays = request.DurationInDays,
            FeatureLimitJson = NormalizeJsonString(request.FeatureLimitJson),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        _subscriptionPlanRepository.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateAsync(entity.Id, CancellationToken.None);

        return MapToResponse(entity, Array.Empty<SubscriptionPlanQuotaResponse>());
    }

    public async Task<SubscriptionPlanResponse?> UpdateSubscriptionPlanAsync(
        Guid id,
        UpdateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        if (request is null)
        {
            throw new ArgumentException("Request is required.");
        }

        var entity = await _subscriptionPlanRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return null;
        }

        if (request.PlanName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.PlanName))
            {
                throw new ArgumentException("Plan name cannot be empty when provided.");
            }

            var normalizedPlanName = request.PlanName.Trim();
            if (!string.Equals(entity.PlanName, normalizedPlanName, StringComparison.OrdinalIgnoreCase))
            {
                var normalizedPlanNameLower = normalizedPlanName.ToLowerInvariant();
                var duplicatedPlan = await _subscriptionPlanRepository.FirstOrDefaultAsync(
                    x => !x.IsDeleted
                         && x.Id != id
                         && x.PlanName != null
                         && x.PlanName.ToLower() == normalizedPlanNameLower,
                    asNoTracking: true,
                    cancellationToken);

                if (duplicatedPlan is not null)
                {
                    throw new InvalidOperationException("Plan name already exists.");
                }
            }

            entity.PlanName = normalizedPlanName;
        }

        if (request.Price.HasValue)
        {
            if (request.Price.Value < 0)
            {
                throw new ArgumentException("Price must be greater than or equal to 0.");
            }

            entity.Price = request.Price.Value;
        }

        if (request.DurationInDays.HasValue)
        {
            if (request.DurationInDays.Value <= 0)
            {
                throw new ArgumentException("DurationInDays must be greater than 0.");
            }

            entity.DurationInDays = request.DurationInDays.Value;
        }

        if (request.FeatureLimitJson is not null)
        {
            if (!IsValidJson(request.FeatureLimitJson))
            {
                throw new ArgumentException("FeatureLimitJson must be valid JSON.");
            }

            entity.FeatureLimitJson = NormalizeJsonString(request.FeatureLimitJson);
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        entity.UpdatedAt = DateTime.UtcNow;

        _subscriptionPlanRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateAsync(id, CancellationToken.None);

        var quotasByPlan = await _quotaService.GetActivePlanQuotasAsync(
            new[] { entity.Id },
            CancellationToken.None);
        return MapToResponse(entity, GetQuotas(quotasByPlan, entity.Id));
    }

    public async Task<SubscriptionPlanResponse?> UpdateSubscriptionPlanStatusAsync(
        Guid id,
        UpdateSubscriptionPlanStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        if (request is null)
        {
            throw new ArgumentException("Request is required.");
        }

        var entity = await _subscriptionPlanRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return null;
        }

        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        _subscriptionPlanRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateAsync(id, CancellationToken.None);

        var quotasByPlan = await _quotaService.GetActivePlanQuotasAsync(
            new[] { entity.Id },
            CancellationToken.None);
        return MapToResponse(entity, GetQuotas(quotasByPlan, entity.Id));
    }

    public async Task<bool> DeleteSubscriptionPlanAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return false;
        }

        var entity = await _subscriptionPlanRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null || entity.IsDeleted)
        {
            return false;
        }

        var utcNow = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = utcNow;
        entity.UpdatedAt = utcNow;

        _subscriptionPlanRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateAsync(id, CancellationToken.None);

        return true;
    }

    private static SubscriptionPlanResponse MapToResponse(
        SubscriptionPlan entity,
        IReadOnlyList<SubscriptionPlanQuotaResponse> quotas)
    {
        return new SubscriptionPlanResponse
        {
            Id = entity.Id,
            PlanName = entity.PlanName,
            Price = entity.Price,
            DurationInDays = entity.DurationInDays,
            FeatureLimitJson = entity.FeatureLimitJson,
            IsActive = entity.IsActive,
            Quotas = quotas,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? NormalizeJsonString(string? json)
    {
        return string.IsNullOrWhiteSpace(json) ? null : json.Trim();
    }

    private static IReadOnlyList<SubscriptionPlanQuotaResponse> GetQuotas(
        IReadOnlyDictionary<Guid, IReadOnlyList<SubscriptionPlanQuotaResponse>> quotasByPlan,
        Guid planId)
    {
        return quotasByPlan.TryGetValue(planId, out var quotas)
            ? quotas
            : Array.Empty<SubscriptionPlanQuotaResponse>();
    }
}
