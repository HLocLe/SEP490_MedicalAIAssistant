using MedMateAI.Application.DTOs.Quotas.Responses;
using MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Requests;
using MedMateAI.Application.DTOs.SubscriptionPlanQuotas.Responses;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.SubscriptionPlanQuotas;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;
using MedMateAI.Domain.Repository;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Application.Service;

public sealed class SubscriptionPlanQuotaService
    : ISubscriptionPlanQuotaService
{
    private readonly ISubscriptionPlanQuotaRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubscriptionPlanCacheInvalidator _cacheInvalidator;
    private readonly ILogger<SubscriptionPlanQuotaService> _logger;

    public SubscriptionPlanQuotaService(
        ISubscriptionPlanQuotaRepository repository,
        IUnitOfWork unitOfWork,
        ISubscriptionPlanCacheInvalidator cacheInvalidator,
        ILogger<SubscriptionPlanQuotaService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cacheInvalidator = cacheInvalidator;
        _logger = logger;
    }

    public async Task<SubscriptionPlanQuotaOperationResult<IReadOnlyList<QuotaResponse>>>
        ListQuotaDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var quotas = await _repository.ListQuotaDefinitionsAsync(cancellationToken);
            var response = quotas
                .Select(MapQuota)
                .OrderBy(quota => quota.Code, StringComparer.OrdinalIgnoreCase)
                .ThenBy(quota => quota.Id)
                .ToList();

            return SubscriptionPlanQuotaOperationResult<IReadOnlyList<QuotaResponse>>
                .Ok(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailure("ListQuotaDefinitions", ex);
            return SubscriptionPlanQuotaOperationResult<IReadOnlyList<QuotaResponse>>
                .Fail(
                    SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaConflict,
                    "Quota definitions could not be loaded.");
        }
    }

    public async Task<SubscriptionPlanQuotaOperationResult<IReadOnlyList<SubscriptionPlanQuotaResponse>>>
        ListPlanQuotasAsync(
            Guid planId,
            CancellationToken cancellationToken = default)
    {
        if (planId == Guid.Empty)
        {
            return SubscriptionPlanQuotaOperationResult<IReadOnlyList<SubscriptionPlanQuotaResponse>>
                .Fail(
                    SubscriptionPlanQuotaErrorCode.InvalidRequest,
                    "Invalid subscription plan id.");
        }

        try
        {
            var plan = await _unitOfWork.SubscriptionPlans.FirstOrDefaultAsync(
                currentPlan => currentPlan.Id == planId && !currentPlan.IsDeleted,
                asNoTracking: true,
                cancellationToken);
            if (plan is null)
            {
                return SubscriptionPlanQuotaOperationResult<IReadOnlyList<SubscriptionPlanQuotaResponse>>
                    .Fail(
                        SubscriptionPlanQuotaErrorCode.SubscriptionPlanNotFound,
                        "Subscription plan was not found.");
            }

            var mappings = await _repository.ListPlanQuotasAsync(
                planId,
                cancellationToken);
            var response = mappings
                .Select(mapping => MapPlanQuota(mapping, mapping.Quota))
                .OrderBy(mapping => mapping.QuotaCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(mapping => mapping.QuotaId)
                .ToList();

            return SubscriptionPlanQuotaOperationResult<IReadOnlyList<SubscriptionPlanQuotaResponse>>
                .Ok(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailure("ListPlanQuotas", ex, planId);
            return SubscriptionPlanQuotaOperationResult<IReadOnlyList<SubscriptionPlanQuotaResponse>>
                .Fail(
                    SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaConflict,
                    "Subscription plan quotas could not be loaded.");
        }
    }

    public async Task<SubscriptionPlanQuotaOperationResult<SubscriptionPlanQuotaResponse>>
        UpsertPlanQuotaAsync(
            Guid planId,
            Guid quotaId,
            UpsertSubscriptionPlanQuotaRequest request,
            CancellationToken cancellationToken = default)
    {
        var validationError = ValidateUpsertRequest(planId, quotaId, request);
        if (validationError is not null)
        {
            return validationError;
        }

        _unitOfWork.ClearTrackedChanges();
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var plan = await _repository.GetPlanForUpdateAsync(planId, cancellationToken);
            if (plan is null)
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return SubscriptionPlanQuotaOperationResult<SubscriptionPlanQuotaResponse>
                    .Fail(
                        SubscriptionPlanQuotaErrorCode.SubscriptionPlanNotFound,
                        "Subscription plan was not found.");
            }

            var quota = await _repository.GetQuotaDefinitionAsync(quotaId, cancellationToken);
            if (quota is null)
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return SubscriptionPlanQuotaOperationResult<SubscriptionPlanQuotaResponse>
                    .Fail(
                        SubscriptionPlanQuotaErrorCode.QuotaNotFound,
                        "Quota definition was not found.");
            }

            if (!quota.IsActive)
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return SubscriptionPlanQuotaOperationResult<SubscriptionPlanQuotaResponse>
                    .Fail(
                        SubscriptionPlanQuotaErrorCode.QuotaInactive,
                        "Inactive quota definitions cannot be assigned to a plan.");
            }

            var utcNow = DateTime.UtcNow;
            var mapping = await _repository.GetNonDeletedMappingAsync(
                planId,
                quotaId,
                cancellationToken);

            if (mapping is null)
            {
                mapping = await _repository.GetLatestDeletedMappingAsync(
                    planId,
                    quotaId,
                    cancellationToken);
            }

            if (mapping is null)
            {
                mapping = CreateMapping(planId, quotaId, request, utcNow);
                _repository.Add(mapping);
            }
            else
            {
                ApplyUpsert(mapping, request, utcNow);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            await _cacheInvalidator.InvalidateAsync(
                planId,
                CancellationToken.None);

            return SubscriptionPlanQuotaOperationResult<SubscriptionPlanQuotaResponse>
                .Ok(MapPlanQuota(mapping, quota));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            LogFailure("UpsertPlanQuota", ex, planId, quotaId);
            return SubscriptionPlanQuotaOperationResult<SubscriptionPlanQuotaResponse>
                .Fail(
                    SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaConflict,
                    "Subscription plan quota could not be saved.");
        }
    }

    public async Task<SubscriptionPlanQuotaOperationResult<bool>> DeletePlanQuotaAsync(
        Guid planId,
        Guid quotaId,
        CancellationToken cancellationToken = default)
    {
        if (planId == Guid.Empty || quotaId == Guid.Empty)
        {
            return SubscriptionPlanQuotaOperationResult<bool>.Fail(
                SubscriptionPlanQuotaErrorCode.InvalidRequest,
                "Invalid subscription plan or quota id.");
        }

        _unitOfWork.ClearTrackedChanges();
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var plan = await _repository.GetPlanForUpdateAsync(planId, cancellationToken);
            if (plan is null)
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return SubscriptionPlanQuotaOperationResult<bool>.Fail(
                    SubscriptionPlanQuotaErrorCode.SubscriptionPlanNotFound,
                    "Subscription plan was not found.");
            }

            var mapping = await _repository.GetNonDeletedMappingAsync(
                planId,
                quotaId,
                cancellationToken);
            if (mapping is null)
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return SubscriptionPlanQuotaOperationResult<bool>.Fail(
                    SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaNotFound,
                    "Subscription plan quota was not found.");
            }

            var utcNow = DateTime.UtcNow;
            mapping.IsDeleted = true;
            mapping.DeletedAt = utcNow;
            mapping.UpdatedAt = utcNow;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            await _cacheInvalidator.InvalidateAsync(
                planId,
                CancellationToken.None);

            return SubscriptionPlanQuotaOperationResult<bool>.Ok(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            LogFailure("DeletePlanQuota", ex, planId, quotaId);
            return SubscriptionPlanQuotaOperationResult<bool>.Fail(
                SubscriptionPlanQuotaErrorCode.SubscriptionPlanQuotaConflict,
                "Subscription plan quota could not be deleted.");
        }
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<SubscriptionPlanQuotaResponse>>>
        GetActivePlanQuotasAsync(
            IReadOnlyCollection<Guid> planIds,
            CancellationToken cancellationToken = default)
    {
        if (planIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<SubscriptionPlanQuotaResponse>>();
        }

        var normalizedPlanIds = planIds.Distinct().ToArray();
        var mappings = await _repository.ListActivePlanQuotasAsync(
            normalizedPlanIds,
            cancellationToken);
        var groupedMappings = mappings
            .GroupBy(mapping => mapping.PlanId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SubscriptionPlanQuotaResponse>)group
                    .Select(mapping => MapPlanQuota(mapping, mapping.Quota))
                    .OrderBy(response => response.QuotaCode, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(response => response.QuotaId)
                    .ToList());

        return normalizedPlanIds.ToDictionary(
            planId => planId,
            planId => groupedMappings.TryGetValue(planId, out var quotas)
                ? quotas
                : (IReadOnlyList<SubscriptionPlanQuotaResponse>)Array.Empty<SubscriptionPlanQuotaResponse>());
    }

    private static SubscriptionPlanQuotaOperationResult<SubscriptionPlanQuotaResponse>?
        ValidateUpsertRequest(
            Guid planId,
            Guid quotaId,
            UpsertSubscriptionPlanQuotaRequest? request)
    {
        if (planId == Guid.Empty
            || quotaId == Guid.Empty
            || request is null
            || request.LimitValue < 0
            || !Enum.IsDefined(typeof(QuotaResetPeriod), request.ResetPeriod)
            || request.ResetPeriod != QuotaResetPeriod.SubscriptionCycle)
        {
            return SubscriptionPlanQuotaOperationResult<SubscriptionPlanQuotaResponse>
                .Fail(
                    SubscriptionPlanQuotaErrorCode.InvalidRequest,
                    "Invalid subscription plan quota request.");
        }

        return null;
    }

    private static SubscriptionPlanQuota CreateMapping(
        Guid planId,
        Guid quotaId,
        UpsertSubscriptionPlanQuotaRequest request,
        DateTime utcNow)
    {
        return new SubscriptionPlanQuota
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            QuotaId = quotaId,
            LimitValue = request.LimitValue,
            ResetPeriod = request.ResetPeriod,
            IsActive = request.IsActive,
            CreatedAt = utcNow,
        };
    }

    private static void ApplyUpsert(
        SubscriptionPlanQuota mapping,
        UpsertSubscriptionPlanQuotaRequest request,
        DateTime utcNow)
    {
        var changed = mapping.IsDeleted
            || mapping.LimitValue != request.LimitValue
            || mapping.ResetPeriod != request.ResetPeriod
            || mapping.IsActive != request.IsActive;

        if (!changed)
        {
            return;
        }

        mapping.LimitValue = request.LimitValue;
        mapping.ResetPeriod = request.ResetPeriod;
        mapping.IsActive = request.IsActive;
        mapping.IsDeleted = false;
        mapping.DeletedAt = null;
        mapping.UpdatedAt = utcNow;
    }

    private static QuotaResponse MapQuota(Quota quota)
    {
        return new QuotaResponse
        {
            Id = quota.Id,
            Code = quota.Code,
            Name = quota.Name,
            Description = quota.Description,
            Unit = quota.Unit,
            IsActive = quota.IsActive,
            CreatedAt = quota.CreatedAt,
            UpdatedAt = quota.UpdatedAt,
        };
    }

    private static SubscriptionPlanQuotaResponse MapPlanQuota(
        SubscriptionPlanQuota mapping,
        Quota quota)
    {
        return new SubscriptionPlanQuotaResponse
        {
            Id = mapping.Id,
            PlanId = mapping.PlanId,
            QuotaId = mapping.QuotaId,
            QuotaCode = quota.Code,
            QuotaName = quota.Name,
            QuotaDescription = quota.Description,
            Unit = quota.Unit,
            LimitValue = mapping.LimitValue,
            ResetPeriod = mapping.ResetPeriod,
            IsActive = mapping.IsActive,
            CreatedAt = mapping.CreatedAt,
            UpdatedAt = mapping.UpdatedAt,
        };
    }

    private void LogFailure(
        string operation,
        Exception exception,
        Guid? planId = null,
        Guid? quotaId = null)
    {
        _logger.LogWarning(
            "Subscription plan quota operation {Operation} failed for plan {PlanId} and quota {QuotaId}; category {ErrorCategory}.",
            operation,
            planId,
            quotaId,
            exception.GetType().Name);
    }
}
