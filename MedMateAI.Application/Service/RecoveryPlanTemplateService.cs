using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.DTOs.RecoveryPlanTemplates;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class RecoveryPlanTemplateService : IRecoveryPlanTemplateService
{
    private readonly IUnitOfWork _unitOfWork;

    public RecoveryPlanTemplateService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<
        RecoveryPlanOperationResult<PagedResponse<RecoveryPlanTemplateSummaryResponse>>>
        GetPagedAsync(
            Guid doctorUserId,
            PaginationQuery page,
            RecoveryPlanDiseaseGroup? diseaseGroup,
            string? search,
            CancellationToken cancellationToken)
    {
        if (diseaseGroup.HasValue && !Enum.IsDefined(diseaseGroup.Value))
        {
            return RecoveryPlanOperationResult<
                PagedResponse<RecoveryPlanTemplateSummaryResponse>>.Fail(
                    RecoveryPlanErrorCode.InvalidRequest);
        }

        var doctorResult = await GetActiveDoctorAsync(doctorUserId, cancellationToken);
        if (doctorResult.Error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<
                PagedResponse<RecoveryPlanTemplateSummaryResponse>>.Fail(
                    doctorResult.Error);
        }

        var pageResult = await _unitOfWork.RecoveryPlanTemplates.GetPagedAsync(
            doctorResult.Doctor!.Id,
            page.PageNumber,
            page.PageSize,
            diseaseGroup,
            RecoveryPlanValidation.NormalizeOptional(search),
            cancellationToken);

        return RecoveryPlanOperationResult<
            PagedResponse<RecoveryPlanTemplateSummaryResponse>>.Ok(
                RecoveryPlanTemplateMapping.ToPage(pageResult));
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>>
        GetDetailAsync(
            Guid doctorUserId,
            Guid templateId,
            CancellationToken cancellationToken)
    {
        var doctorResult = await GetActiveDoctorAsync(doctorUserId, cancellationToken);
        if (doctorResult.Error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>.Fail(
                doctorResult.Error);
        }

        var template = await _unitOfWork.RecoveryPlanTemplates.GetDetailAsync(
            doctorResult.Doctor!.Id,
            templateId,
            cancellationToken);

        return template is null
            ? RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>.Fail(
                RecoveryPlanErrorCode.NotFound)
            : RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>.Ok(
                RecoveryPlanTemplateMapping.ToDetail(template));
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>>
        CreateAsync(
            Guid doctorUserId,
            CreateRecoveryPlanTemplateRequest request,
            CancellationToken cancellationToken)
    {
        var templateName = request.TemplateName?.Trim() ?? string.Empty;
        var planName = request.PlanName?.Trim() ?? string.Empty;
        var summary = RecoveryPlanValidation.NormalizeOptional(request.Summary);
        var recheckInstruction = RecoveryPlanValidation.NormalizeOptional(
            request.RecheckInstruction);
        var validationError = RecoveryPlanTemplateValidation.ValidateHeader(
            templateName,
            request,
            planName,
            summary,
            recheckInstruction);

        if (validationError != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>.Fail(
                validationError);
        }

        var doctorResult = await GetActiveDoctorAsync(doctorUserId, cancellationToken);
        if (doctorResult.Error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>.Fail(
                doctorResult.Error);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var template = new RecoveryPlanTemplate
            {
                Id = Guid.NewGuid(),
                DoctorId = doctorResult.Doctor!.Id,
                DiseaseGroup = request.DiseaseGroup,
                TemplateName = templateName,
                PlanName = planName,
                DurationDays = request.DurationDays,
                Summary = summary,
                RecheckInstruction = recheckInstruction,
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.RecoveryPlanTemplates.Add(template);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var response = RecoveryPlanTemplateMapping.ToDetail(template);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>.Ok(
                response);
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanTemplateDetailResponse>> UpdateAsync(
        Guid doctorUserId,
        Guid templateId,
        UpdateRecoveryPlanTemplateRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            (template, utcNow) => RecoveryPlanTemplateMutations.UpdateHeader(
                template,
                request,
                utcNow),
            RecoveryPlanTemplateMapping.ToDetail,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<bool>> DeleteAsync(
        Guid doctorUserId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            RecoveryPlanTemplateMutations.DeleteTemplate,
            static deleted => deleted,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanTemplatePhaseResponse>>
        CreatePhaseAsync(
            Guid doctorUserId,
            Guid templateId,
            UpsertRecoveryPlanPhaseRequest request,
            CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            (template, utcNow) => RecoveryPlanTemplateMutations.CreatePhase(
                template,
                request,
                utcNow),
            RecoveryPlanTemplateMapping.ToPhase,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanTemplatePhaseResponse>>
        UpdatePhaseAsync(
            Guid doctorUserId,
            Guid templateId,
            Guid phaseId,
            UpsertRecoveryPlanPhaseRequest request,
            CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            (template, utcNow) => RecoveryPlanTemplateMutations.UpdatePhase(
                template,
                phaseId,
                request,
                utcNow),
            RecoveryPlanTemplateMapping.ToPhase,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<bool>> DeletePhaseAsync(
        Guid doctorUserId,
        Guid templateId,
        Guid phaseId,
        CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            (template, utcNow) => RecoveryPlanTemplateMutations.DeletePhase(
                template,
                phaseId,
                utcNow),
            static deleted => deleted,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTargetResponse>>
        CreateNutrientAsync(
            Guid doctorUserId,
            Guid templateId,
            Guid phaseId,
            UpsertRecoveryPlanNutrientTargetRequest request,
            CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            (template, utcNow) => RecoveryPlanTemplateMutations.CreateNutrient(
                template,
                phaseId,
                request,
                utcNow),
            RecoveryPlanTemplateMapping.ToNutrient,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTargetResponse>>
        UpdateNutrientAsync(
            Guid doctorUserId,
            Guid templateId,
            Guid phaseId,
            Guid nutrientId,
            UpsertRecoveryPlanNutrientTargetRequest request,
            CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            (template, utcNow) => RecoveryPlanTemplateMutations.UpdateNutrient(
                template,
                phaseId,
                nutrientId,
                request,
                utcNow),
            RecoveryPlanTemplateMapping.ToNutrient,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<bool>> DeleteNutrientAsync(
        Guid doctorUserId,
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            (template, utcNow) => RecoveryPlanTemplateMutations.DeleteNutrient(
                template,
                phaseId,
                nutrientId,
                utcNow),
            static deleted => deleted,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSourceResponse>>
        CreateFoodAsync(
            Guid doctorUserId,
            Guid templateId,
            Guid phaseId,
            Guid nutrientId,
            UpsertRecoveryPlanFoodSourceRequest request,
            CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            (template, utcNow) => RecoveryPlanTemplateMutations.CreateFood(
                template,
                phaseId,
                nutrientId,
                request,
                utcNow),
            RecoveryPlanTemplateMapping.ToFood,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSourceResponse>>
        UpdateFoodAsync(
            Guid doctorUserId,
            Guid templateId,
            Guid phaseId,
            Guid nutrientId,
            Guid foodSourceId,
            UpsertRecoveryPlanFoodSourceRequest request,
            CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            (template, utcNow) => RecoveryPlanTemplateMutations.UpdateFood(
                template,
                phaseId,
                nutrientId,
                foodSourceId,
                request,
                utcNow),
            RecoveryPlanTemplateMapping.ToFood,
            cancellationToken);
    }

    public Task<RecoveryPlanOperationResult<bool>> DeleteFoodAsync(
        Guid doctorUserId,
        Guid templateId,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        CancellationToken cancellationToken)
    {
        return ExecuteWriteAsync(
            doctorUserId,
            templateId,
            (template, utcNow) => RecoveryPlanTemplateMutations.DeleteFood(
                template,
                phaseId,
                nutrientId,
                foodSourceId,
                utcNow),
            static deleted => deleted,
            cancellationToken);
    }

    private async Task<RecoveryPlanOperationResult<TResponse>> ExecuteWriteAsync<
        TMutation,
        TResponse>(
        Guid doctorUserId,
        Guid templateId,
        Func<RecoveryPlanTemplate, DateTime, RecoveryPlanOperationResult<TMutation>> mutation,
        Func<TMutation, TResponse> mapResponse,
        CancellationToken cancellationToken)
    {
        var doctorResult = await GetActiveDoctorAsync(doctorUserId, cancellationToken);
        if (doctorResult.Error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<TResponse>.Fail(doctorResult.Error);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var doctorId = doctorResult.Doctor!.Id;
            var lockedTemplate = await _unitOfWork.RecoveryPlanTemplates
                .GetByIdForUpdateAsync(doctorId, templateId, cancellationToken);
            if (lockedTemplate is null)
            {
                return await RollbackFailureAsync<TResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            var template = await _unitOfWork.RecoveryPlanTemplates.GetTrackedDetailAsync(
                doctorId,
                templateId,
                cancellationToken);
            if (template is null)
            {
                return await RollbackFailureAsync<TResponse>(
                    RecoveryPlanErrorCode.NotFound);
            }

            var mutationResult = mutation(template, DateTime.UtcNow);
            if (!mutationResult.Success)
            {
                return await RollbackFailureAsync<TResponse>(
                    mutationResult.Error,
                    mutationResult.Message);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var response = mapResponse(mutationResult.Data!);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return RecoveryPlanOperationResult<TResponse>.Ok(response);
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    private async Task<(Doctor? Doctor, RecoveryPlanErrorCode Error)> GetActiveDoctorAsync(
        Guid doctorUserId,
        CancellationToken cancellationToken)
    {
        var doctor = await _unitOfWork.RecoveryPlanRequests.GetDoctorByUserIdAsync(
            doctorUserId,
            cancellationToken);

        if (doctor is null)
        {
            return (null, RecoveryPlanErrorCode.DoctorProfileNotFound);
        }

        return doctor.IsActive
            ? (doctor, RecoveryPlanErrorCode.None)
            : (null, RecoveryPlanErrorCode.DoctorNotActive);
    }

    private async Task<RecoveryPlanOperationResult<TResponse>> RollbackFailureAsync<TResponse>(
        RecoveryPlanErrorCode error,
        string? message = null)
    {
        await RollbackAsync();
        return RecoveryPlanOperationResult<TResponse>.Fail(error, message);
    }

    private Task RollbackAsync()
    {
        return _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
    }
}
