using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.DTOs.RecoveryPlanTemplates;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Service;

internal static class RecoveryPlanTemplateMutations
{
    public static RecoveryPlanOperationResult<RecoveryPlanTemplate> UpdateHeader(
        RecoveryPlanTemplate template,
        UpdateRecoveryPlanTemplateRequest request,
        DateTime utcNow)
    {
        var templateName = request.TemplateName?.Trim() ?? string.Empty;
        var planName = request.PlanName?.Trim() ?? string.Empty;
        var summary = RecoveryPlanValidation.NormalizeOptional(request.Summary);
        var recheckInstruction = RecoveryPlanValidation.NormalizeOptional(
            request.RecheckInstruction);
        var error = RecoveryPlanTemplateValidation.ValidateHeader(
            templateName,
            request,
            planName,
            summary,
            recheckInstruction);

        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplate>.Fail(error);
        }

        if (template.Phases.Any(phase =>
                !phase.IsDeleted
                && phase.EndDay > request.DurationDays))
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplate>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        template.TemplateName = templateName;
        template.DiseaseGroup = request.DiseaseGroup;
        template.PlanName = planName;
        template.DurationDays = request.DurationDays;
        template.Summary = summary;
        template.RecheckInstruction = recheckInstruction;
        template.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanTemplate>.Ok(template);
    }

    public static RecoveryPlanOperationResult<bool> DeleteTemplate(
        RecoveryPlanTemplate template,
        DateTime utcNow)
    {
        foreach (var phase in template.Phases.Where(phase => !phase.IsDeleted))
        {
            SoftDeletePhase(phase, utcNow);
        }

        SoftDelete(template, utcNow);
        return RecoveryPlanOperationResult<bool>.Ok(true);
    }

    public static RecoveryPlanOperationResult<RecoveryPlanTemplatePhase> CreatePhase(
        RecoveryPlanTemplate template,
        UpsertRecoveryPlanPhaseRequest request,
        DateTime utcNow)
    {
        var phaseName = request.PhaseName?.Trim() ?? string.Empty;
        var instruction = RecoveryPlanValidation.NormalizeOptional(request.Instruction);
        var error = RecoveryPlanValidation.ValidatePhase(
            request,
            phaseName,
            instruction,
            template.DurationDays);

        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplatePhase>.Fail(error);
        }

        if (HasPhaseSortOrderConflict(template, request.SortOrder, null)
            || HasPhaseOverlap(template, request.StartDay, request.EndDay, null))
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplatePhase>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        var phase = new RecoveryPlanTemplatePhase
        {
            RecoveryPlanTemplate = template,
            PhaseName = phaseName,
            StartDay = request.StartDay,
            EndDay = request.EndDay,
            SleepAndRestHoursPerDay = request.SleepAndRestHoursPerDay,
            Instruction = instruction,
            SortOrder = request.SortOrder,
            CreatedAt = utcNow
        };

        template.Phases.Add(phase);
        template.UpdatedAt = utcNow;
        return RecoveryPlanOperationResult<RecoveryPlanTemplatePhase>.Ok(phase);
    }

    public static RecoveryPlanOperationResult<RecoveryPlanTemplatePhase> UpdatePhase(
        RecoveryPlanTemplate template,
        Guid phaseId,
        UpsertRecoveryPlanPhaseRequest request,
        DateTime utcNow)
    {
        var phase = FindPhase(template, phaseId);
        if (phase is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplatePhase>.Fail(
                RecoveryPlanErrorCode.NotFound);
        }

        var phaseName = request.PhaseName?.Trim() ?? string.Empty;
        var instruction = RecoveryPlanValidation.NormalizeOptional(request.Instruction);
        var error = RecoveryPlanValidation.ValidatePhase(
            request,
            phaseName,
            instruction,
            template.DurationDays);

        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplatePhase>.Fail(error);
        }

        if (HasPhaseSortOrderConflict(template, request.SortOrder, phase.Id)
            || HasPhaseOverlap(template, request.StartDay, request.EndDay, phase.Id))
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplatePhase>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        phase.PhaseName = phaseName;
        phase.StartDay = request.StartDay;
        phase.EndDay = request.EndDay;
        phase.SleepAndRestHoursPerDay = request.SleepAndRestHoursPerDay;
        phase.Instruction = instruction;
        phase.SortOrder = request.SortOrder;
        phase.UpdatedAt = utcNow;
        template.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanTemplatePhase>.Ok(phase);
    }

    public static RecoveryPlanOperationResult<bool> DeletePhase(
        RecoveryPlanTemplate template,
        Guid phaseId,
        DateTime utcNow)
    {
        var phase = FindPhase(template, phaseId);
        if (phase is null)
        {
            return RecoveryPlanOperationResult<bool>.Fail(RecoveryPlanErrorCode.NotFound);
        }

        SoftDeletePhase(phase, utcNow);
        template.UpdatedAt = utcNow;
        return RecoveryPlanOperationResult<bool>.Ok(true);
    }

    public static RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTarget>
        CreateNutrient(
            RecoveryPlanTemplate template,
            Guid phaseId,
            UpsertRecoveryPlanNutrientTargetRequest request,
            DateTime utcNow)
    {
        var phase = FindPhase(template, phaseId);
        if (phase is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTarget>.Fail(
                RecoveryPlanErrorCode.NotFound);
        }

        var nutrientName = request.NutrientName?.Trim() ?? string.Empty;
        var unit = request.Unit?.Trim() ?? string.Empty;
        var instruction = RecoveryPlanValidation.NormalizeOptional(request.Instruction);
        var error = RecoveryPlanValidation.ValidateNutrient(
            request,
            nutrientName,
            unit,
            instruction);

        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTarget>.Fail(
                error);
        }

        if (phase.NutrientTargets.Any(nutrient =>
                !nutrient.IsDeleted
                && nutrient.SortOrder == request.SortOrder))
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTarget>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        var nutrient = new RecoveryPlanTemplateNutrientTarget
        {
            RecoveryPlanTemplatePhase = phase,
            NutrientName = nutrientName,
            AmountPerDay = request.AmountPerDay,
            Unit = unit,
            Instruction = instruction,
            SortOrder = request.SortOrder,
            CreatedAt = utcNow
        };

        phase.NutrientTargets.Add(nutrient);
        phase.UpdatedAt = utcNow;
        template.UpdatedAt = utcNow;
        return RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTarget>.Ok(nutrient);
    }

    public static RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTarget>
        UpdateNutrient(
            RecoveryPlanTemplate template,
            Guid phaseId,
            Guid nutrientId,
            UpsertRecoveryPlanNutrientTargetRequest request,
            DateTime utcNow)
    {
        var phase = FindPhase(template, phaseId);
        var nutrient = FindNutrient(phase, nutrientId);
        if (phase is null || nutrient is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTarget>.Fail(
                RecoveryPlanErrorCode.NotFound);
        }

        var nutrientName = request.NutrientName?.Trim() ?? string.Empty;
        var unit = request.Unit?.Trim() ?? string.Empty;
        var instruction = RecoveryPlanValidation.NormalizeOptional(request.Instruction);
        var error = RecoveryPlanValidation.ValidateNutrient(
            request,
            nutrientName,
            unit,
            instruction);

        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTarget>.Fail(
                error);
        }

        if (phase.NutrientTargets.Any(currentNutrient =>
                !currentNutrient.IsDeleted
                && currentNutrient.Id != nutrient.Id
                && currentNutrient.SortOrder == request.SortOrder))
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTarget>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        nutrient.NutrientName = nutrientName;
        nutrient.AmountPerDay = request.AmountPerDay;
        nutrient.Unit = unit;
        nutrient.Instruction = instruction;
        nutrient.SortOrder = request.SortOrder;
        nutrient.UpdatedAt = utcNow;
        phase.UpdatedAt = utcNow;
        template.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanTemplateNutrientTarget>.Ok(nutrient);
    }

    public static RecoveryPlanOperationResult<bool> DeleteNutrient(
        RecoveryPlanTemplate template,
        Guid phaseId,
        Guid nutrientId,
        DateTime utcNow)
    {
        var phase = FindPhase(template, phaseId);
        var nutrient = FindNutrient(phase, nutrientId);
        if (phase is null || nutrient is null)
        {
            return RecoveryPlanOperationResult<bool>.Fail(RecoveryPlanErrorCode.NotFound);
        }

        SoftDeleteNutrient(nutrient, utcNow);
        phase.UpdatedAt = utcNow;
        template.UpdatedAt = utcNow;
        return RecoveryPlanOperationResult<bool>.Ok(true);
    }

    public static RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSource> CreateFood(
        RecoveryPlanTemplate template,
        Guid phaseId,
        Guid nutrientId,
        UpsertRecoveryPlanFoodSourceRequest request,
        DateTime utcNow)
    {
        var phase = FindPhase(template, phaseId);
        var nutrient = FindNutrient(phase, nutrientId);
        if (phase is null || nutrient is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSource>.Fail(
                RecoveryPlanErrorCode.NotFound);
        }

        var foodName = request.FoodName?.Trim() ?? string.Empty;
        var suggestedServing = RecoveryPlanValidation.NormalizeOptional(
            request.SuggestedServing);
        var note = RecoveryPlanValidation.NormalizeOptional(request.Note);
        var error = RecoveryPlanValidation.ValidateFood(
            request,
            foodName,
            suggestedServing,
            note);

        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSource>.Fail(error);
        }

        if (nutrient.FoodSources.Any(food =>
                !food.IsDeleted
                && food.SortOrder == request.SortOrder))
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSource>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        var foodSource = new RecoveryPlanTemplateFoodSource
        {
            RecoveryPlanTemplateNutrientTarget = nutrient,
            FoodName = foodName,
            SuggestedServing = suggestedServing,
            Note = note,
            SortOrder = request.SortOrder,
            CreatedAt = utcNow
        };

        nutrient.FoodSources.Add(foodSource);
        nutrient.UpdatedAt = utcNow;
        phase.UpdatedAt = utcNow;
        template.UpdatedAt = utcNow;
        return RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSource>.Ok(foodSource);
    }

    public static RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSource> UpdateFood(
        RecoveryPlanTemplate template,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        UpsertRecoveryPlanFoodSourceRequest request,
        DateTime utcNow)
    {
        var phase = FindPhase(template, phaseId);
        var nutrient = FindNutrient(phase, nutrientId);
        var food = FindFood(nutrient, foodSourceId);
        if (phase is null || nutrient is null || food is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSource>.Fail(
                RecoveryPlanErrorCode.NotFound);
        }

        var foodName = request.FoodName?.Trim() ?? string.Empty;
        var suggestedServing = RecoveryPlanValidation.NormalizeOptional(
            request.SuggestedServing);
        var note = RecoveryPlanValidation.NormalizeOptional(request.Note);
        var error = RecoveryPlanValidation.ValidateFood(
            request,
            foodName,
            suggestedServing,
            note);

        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSource>.Fail(error);
        }

        if (nutrient.FoodSources.Any(currentFood =>
                !currentFood.IsDeleted
                && currentFood.Id != food.Id
                && currentFood.SortOrder == request.SortOrder))
        {
            return RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSource>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        food.FoodName = foodName;
        food.SuggestedServing = suggestedServing;
        food.Note = note;
        food.SortOrder = request.SortOrder;
        food.UpdatedAt = utcNow;
        nutrient.UpdatedAt = utcNow;
        phase.UpdatedAt = utcNow;
        template.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanTemplateFoodSource>.Ok(food);
    }

    public static RecoveryPlanOperationResult<bool> DeleteFood(
        RecoveryPlanTemplate template,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        DateTime utcNow)
    {
        var phase = FindPhase(template, phaseId);
        var nutrient = FindNutrient(phase, nutrientId);
        var food = FindFood(nutrient, foodSourceId);
        if (phase is null || nutrient is null || food is null)
        {
            return RecoveryPlanOperationResult<bool>.Fail(RecoveryPlanErrorCode.NotFound);
        }

        SoftDelete(food, utcNow);
        nutrient.UpdatedAt = utcNow;
        phase.UpdatedAt = utcNow;
        template.UpdatedAt = utcNow;
        return RecoveryPlanOperationResult<bool>.Ok(true);
    }

    private static RecoveryPlanTemplatePhase? FindPhase(
        RecoveryPlanTemplate template,
        Guid phaseId)
    {
        return template.Phases.FirstOrDefault(phase =>
            phase.Id == phaseId
            && !phase.IsDeleted);
    }

    private static RecoveryPlanTemplateNutrientTarget? FindNutrient(
        RecoveryPlanTemplatePhase? phase,
        Guid nutrientId)
    {
        return phase?.NutrientTargets.FirstOrDefault(nutrient =>
            nutrient.Id == nutrientId
            && !nutrient.IsDeleted);
    }

    private static RecoveryPlanTemplateFoodSource? FindFood(
        RecoveryPlanTemplateNutrientTarget? nutrient,
        Guid foodSourceId)
    {
        return nutrient?.FoodSources.FirstOrDefault(food =>
            food.Id == foodSourceId
            && !food.IsDeleted);
    }

    private static bool HasPhaseSortOrderConflict(
        RecoveryPlanTemplate template,
        int sortOrder,
        Guid? excludedPhaseId)
    {
        return template.Phases.Any(phase =>
            !phase.IsDeleted
            && phase.Id != excludedPhaseId
            && phase.SortOrder == sortOrder);
    }

    private static bool HasPhaseOverlap(
        RecoveryPlanTemplate template,
        int startDay,
        int endDay,
        Guid? excludedPhaseId)
    {
        return template.Phases.Any(phase =>
            !phase.IsDeleted
            && phase.Id != excludedPhaseId
            && startDay <= phase.EndDay
            && endDay >= phase.StartDay);
    }

    private static void SoftDeletePhase(
        RecoveryPlanTemplatePhase phase,
        DateTime utcNow)
    {
        foreach (var nutrient in phase.NutrientTargets.Where(nutrient => !nutrient.IsDeleted))
        {
            SoftDeleteNutrient(nutrient, utcNow);
        }

        SoftDelete(phase, utcNow);
    }

    private static void SoftDeleteNutrient(
        RecoveryPlanTemplateNutrientTarget nutrient,
        DateTime utcNow)
    {
        foreach (var food in nutrient.FoodSources.Where(food => !food.IsDeleted))
        {
            SoftDelete(food, utcNow);
        }

        SoftDelete(nutrient, utcNow);
    }

    private static void SoftDelete(BaseEntity entity, DateTime utcNow)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = utcNow;
        entity.UpdatedAt = utcNow;
    }
}
