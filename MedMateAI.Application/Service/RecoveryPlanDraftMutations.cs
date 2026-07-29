using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Service;

internal static class RecoveryPlanDraftMutations
{
    public static RecoveryPlanOperationResult<RecoveryPlanDetailResponse> UpdateHeader(
        RecoveryPlan plan,
        UpdateRecoveryPlanDraftRequest request,
        DateTime utcNow)
    {
        var planName = request.PlanName?.Trim() ?? string.Empty;
        var summary = RecoveryPlanValidation.NormalizeOptional(request.Summary);
        var recheckInstruction = RecoveryPlanValidation.NormalizeOptional(
            request.RecheckInstruction);
        var error = RecoveryPlanValidation.ValidateDraftHeader(
            planName,
            summary,
            request.DurationDays,
            recheckInstruction);

        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Fail(error);
        }

        if (plan.Phases.Any(phase =>
                !phase.IsDeleted
                && phase.EndDay > request.DurationDays))
        {
            return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        plan.PlanName = planName;
        plan.Summary = summary;
        plan.DurationDays = request.DurationDays;
        plan.RecheckInstruction = recheckInstruction;
        plan.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanDetailResponse>.Ok(
            RecoveryPlanMapping.ToDetail(plan));
    }

    public static RecoveryPlanOperationResult<bool> DeletePlan(
        RecoveryPlan plan,
        DateTime utcNow)
    {
        foreach (var phase in plan.Phases.Where(phase => !phase.IsDeleted))
        {
            SoftDeletePhase(phase, utcNow);
        }

        SoftDelete(plan, utcNow);
        return RecoveryPlanOperationResult<bool>.Ok(true);
    }

    public static RecoveryPlanOperationResult<RecoveryPlanPhaseResponse> CreatePhase(
        RecoveryPlan plan,
        UpsertRecoveryPlanPhaseRequest request,
        DateTime utcNow)
    {
        var phaseName = request.PhaseName?.Trim() ?? string.Empty;
        var instruction = RecoveryPlanValidation.NormalizeOptional(request.Instruction);
        var error = RecoveryPlanValidation.ValidatePhase(
            request,
            phaseName,
            instruction,
            plan.DurationDays);

        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>.Fail(error);
        }

        if (HasPhaseSortOrderConflict(plan, request.SortOrder, null)
            || HasPhaseOverlap(plan, request.StartDay, request.EndDay, null))
        {
            return RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        var phase = new RecoveryPlanPhase
        {
            Id = Guid.NewGuid(),
            RecoveryPlanId = plan.Id,
            PhaseName = phaseName,
            StartDay = request.StartDay,
            EndDay = request.EndDay,
            SleepHoursPerDay = request.SleepHoursPerDay,
            RestHoursPerDay = request.RestHoursPerDay,
            Instruction = instruction,
            SortOrder = request.SortOrder,
            CreatedAt = utcNow
        };

        plan.Phases.Add(phase);
        plan.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>.Ok(
            RecoveryPlanMapping.ToPhase(phase));
    }

    public static RecoveryPlanOperationResult<RecoveryPlanPhaseResponse> UpdatePhase(
        RecoveryPlan plan,
        Guid phaseId,
        UpsertRecoveryPlanPhaseRequest request,
        DateTime utcNow)
    {
        var phase = FindPhase(plan, phaseId);
        if (phase is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>.Fail(
                RecoveryPlanErrorCode.NotFound);
        }

        var phaseName = request.PhaseName?.Trim() ?? string.Empty;
        var instruction = RecoveryPlanValidation.NormalizeOptional(request.Instruction);
        var error = RecoveryPlanValidation.ValidatePhase(
            request,
            phaseName,
            instruction,
            plan.DurationDays);

        if (error != RecoveryPlanErrorCode.None)
        {
            return RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>.Fail(error);
        }

        if (HasPhaseSortOrderConflict(plan, request.SortOrder, phase.Id)
            || HasPhaseOverlap(plan, request.StartDay, request.EndDay, phase.Id))
        {
            return RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        phase.PhaseName = phaseName;
        phase.StartDay = request.StartDay;
        phase.EndDay = request.EndDay;
        phase.SleepHoursPerDay = request.SleepHoursPerDay;
        phase.RestHoursPerDay = request.RestHoursPerDay;
        phase.Instruction = instruction;
        phase.SortOrder = request.SortOrder;
        phase.UpdatedAt = utcNow;
        plan.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanPhaseResponse>.Ok(
            RecoveryPlanMapping.ToPhase(phase));
    }

    public static RecoveryPlanOperationResult<bool> DeletePhase(
        RecoveryPlan plan,
        Guid phaseId,
        DateTime utcNow)
    {
        var phase = FindPhase(plan, phaseId);
        if (phase is null)
        {
            return RecoveryPlanOperationResult<bool>.Fail(RecoveryPlanErrorCode.NotFound);
        }

        SoftDeletePhase(phase, utcNow);
        plan.UpdatedAt = utcNow;
        return RecoveryPlanOperationResult<bool>.Ok(true);
    }

    public static RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>
        CreateNutrient(
            RecoveryPlan plan,
            Guid phaseId,
            UpsertRecoveryPlanNutrientTargetRequest request,
            DateTime utcNow)
    {
        var phase = FindPhase(plan, phaseId);
        if (phase is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>.Fail(
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
            return RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>.Fail(error);
        }

        if (phase.NutrientTargets.Any(nutrient =>
                !nutrient.IsDeleted
                && nutrient.SortOrder == request.SortOrder))
        {
            return RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        var nutrient = new RecoveryPlanNutrientTarget
        {
            Id = Guid.NewGuid(),
            RecoveryPlanPhaseId = phase.Id,
            NutrientName = nutrientName,
            AmountPerDay = request.AmountPerDay,
            Unit = unit,
            Instruction = instruction,
            SortOrder = request.SortOrder,
            CreatedAt = utcNow
        };

        phase.NutrientTargets.Add(nutrient);
        phase.UpdatedAt = utcNow;
        plan.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>.Ok(
            RecoveryPlanMapping.ToNutrient(nutrient));
    }

    public static RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>
        UpdateNutrient(
            RecoveryPlan plan,
            Guid phaseId,
            Guid nutrientId,
            UpsertRecoveryPlanNutrientTargetRequest request,
            DateTime utcNow)
    {
        var phase = FindPhase(plan, phaseId);
        var nutrient = FindNutrient(phase, nutrientId);
        if (phase is null || nutrient is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>.Fail(
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
            return RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>.Fail(error);
        }

        if (phase.NutrientTargets.Any(currentNutrient =>
                !currentNutrient.IsDeleted
                && currentNutrient.Id != nutrient.Id
                && currentNutrient.SortOrder == request.SortOrder))
        {
            return RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        nutrient.NutrientName = nutrientName;
        nutrient.AmountPerDay = request.AmountPerDay;
        nutrient.Unit = unit;
        nutrient.Instruction = instruction;
        nutrient.SortOrder = request.SortOrder;
        nutrient.UpdatedAt = utcNow;
        phase.UpdatedAt = utcNow;
        plan.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanNutrientTargetResponse>.Ok(
            RecoveryPlanMapping.ToNutrient(nutrient));
    }

    public static RecoveryPlanOperationResult<bool> DeleteNutrient(
        RecoveryPlan plan,
        Guid phaseId,
        Guid nutrientId,
        DateTime utcNow)
    {
        var phase = FindPhase(plan, phaseId);
        var nutrient = FindNutrient(phase, nutrientId);
        if (phase is null || nutrient is null)
        {
            return RecoveryPlanOperationResult<bool>.Fail(RecoveryPlanErrorCode.NotFound);
        }

        SoftDeleteNutrient(nutrient, utcNow);
        phase.UpdatedAt = utcNow;
        plan.UpdatedAt = utcNow;
        return RecoveryPlanOperationResult<bool>.Ok(true);
    }

    public static RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse> CreateFood(
        RecoveryPlan plan,
        Guid phaseId,
        Guid nutrientId,
        UpsertRecoveryPlanFoodSourceRequest request,
        DateTime utcNow)
    {
        var phase = FindPhase(plan, phaseId);
        var nutrient = FindNutrient(phase, nutrientId);
        if (phase is null || nutrient is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>.Fail(
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
            return RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>.Fail(error);
        }

        if (nutrient.FoodSources.Any(food =>
                !food.IsDeleted
                && food.SortOrder == request.SortOrder))
        {
            return RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        var foodSource = new RecoveryPlanFoodSource
        {
            Id = Guid.NewGuid(),
            RecoveryPlanNutrientTargetId = nutrient.Id,
            FoodName = foodName,
            SuggestedServing = suggestedServing,
            Note = note,
            SortOrder = request.SortOrder,
            CreatedAt = utcNow
        };

        nutrient.FoodSources.Add(foodSource);
        nutrient.UpdatedAt = utcNow;
        phase.UpdatedAt = utcNow;
        plan.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>.Ok(
            RecoveryPlanMapping.ToFood(foodSource));
    }

    public static RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse> UpdateFood(
        RecoveryPlan plan,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        UpsertRecoveryPlanFoodSourceRequest request,
        DateTime utcNow)
    {
        var phase = FindPhase(plan, phaseId);
        var nutrient = FindNutrient(phase, nutrientId);
        var foodSource = FindFood(nutrient, foodSourceId);
        if (phase is null || nutrient is null || foodSource is null)
        {
            return RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>.Fail(
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
            return RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>.Fail(error);
        }

        if (nutrient.FoodSources.Any(currentFood =>
                !currentFood.IsDeleted
                && currentFood.Id != foodSource.Id
                && currentFood.SortOrder == request.SortOrder))
        {
            return RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>.Fail(
                RecoveryPlanErrorCode.InvalidPlanStructure);
        }

        foodSource.FoodName = foodName;
        foodSource.SuggestedServing = suggestedServing;
        foodSource.Note = note;
        foodSource.SortOrder = request.SortOrder;
        foodSource.UpdatedAt = utcNow;
        nutrient.UpdatedAt = utcNow;
        phase.UpdatedAt = utcNow;
        plan.UpdatedAt = utcNow;

        return RecoveryPlanOperationResult<RecoveryPlanFoodSourceResponse>.Ok(
            RecoveryPlanMapping.ToFood(foodSource));
    }

    public static RecoveryPlanOperationResult<bool> DeleteFood(
        RecoveryPlan plan,
        Guid phaseId,
        Guid nutrientId,
        Guid foodSourceId,
        DateTime utcNow)
    {
        var phase = FindPhase(plan, phaseId);
        var nutrient = FindNutrient(phase, nutrientId);
        var foodSource = FindFood(nutrient, foodSourceId);
        if (phase is null || nutrient is null || foodSource is null)
        {
            return RecoveryPlanOperationResult<bool>.Fail(RecoveryPlanErrorCode.NotFound);
        }

        SoftDelete(foodSource, utcNow);
        nutrient.UpdatedAt = utcNow;
        phase.UpdatedAt = utcNow;
        plan.UpdatedAt = utcNow;
        return RecoveryPlanOperationResult<bool>.Ok(true);
    }

    private static RecoveryPlanPhase? FindPhase(RecoveryPlan plan, Guid phaseId)
    {
        return plan.Phases.FirstOrDefault(phase =>
            phase.Id == phaseId
            && !phase.IsDeleted);
    }

    private static RecoveryPlanNutrientTarget? FindNutrient(
        RecoveryPlanPhase? phase,
        Guid nutrientId)
    {
        return phase?.NutrientTargets.FirstOrDefault(nutrient =>
            nutrient.Id == nutrientId
            && !nutrient.IsDeleted);
    }

    private static RecoveryPlanFoodSource? FindFood(
        RecoveryPlanNutrientTarget? nutrient,
        Guid foodSourceId)
    {
        return nutrient?.FoodSources.FirstOrDefault(food =>
            food.Id == foodSourceId
            && !food.IsDeleted);
    }

    private static bool HasPhaseSortOrderConflict(
        RecoveryPlan plan,
        int sortOrder,
        Guid? excludedPhaseId)
    {
        return plan.Phases.Any(phase =>
            !phase.IsDeleted
            && phase.Id != excludedPhaseId
            && phase.SortOrder == sortOrder);
    }

    private static bool HasPhaseOverlap(
        RecoveryPlan plan,
        int startDay,
        int endDay,
        Guid? excludedPhaseId)
    {
        return plan.Phases.Any(phase =>
            !phase.IsDeleted
            && phase.Id != excludedPhaseId
            && startDay <= phase.EndDay
            && endDay >= phase.StartDay);
    }

    private static void SoftDeletePhase(RecoveryPlanPhase phase, DateTime utcNow)
    {
        foreach (var nutrient in phase.NutrientTargets.Where(nutrient => !nutrient.IsDeleted))
        {
            SoftDeleteNutrient(nutrient, utcNow);
        }

        SoftDelete(phase, utcNow);
    }

    private static void SoftDeleteNutrient(
        RecoveryPlanNutrientTarget nutrient,
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
