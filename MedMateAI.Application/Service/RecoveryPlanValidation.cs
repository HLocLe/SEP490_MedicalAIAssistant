using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Service;

internal static class RecoveryPlanValidation
{
    public const int MaximumPlanNameLength = 200;
    public const int MaximumSummaryLength = 2000;
    public const int MaximumRecheckInstructionLength = 2000;
    public const int MaximumPhaseNameLength = 200;
    public const int MaximumPhaseInstructionLength = 2000;
    public const int MaximumNutrientNameLength = 200;
    public const int MaximumNutrientUnitLength = 32;
    public const int MaximumNutrientInstructionLength = 1000;
    public const int MaximumFoodNameLength = 256;
    public const int MaximumSuggestedServingLength = 256;
    public const int MaximumFoodNoteLength = 1000;
    public const int MaximumFeedbackNoteLength = 1000;

    public static RecoveryPlanErrorCode ValidateDraftHeader(
        string planName,
        string? summary,
        int durationDays,
        string? recheckInstruction)
    {
        if (planName.Length is < 1 or > MaximumPlanNameLength)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        if (summary?.Length > MaximumSummaryLength)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        if (durationDays is < 1 or > 365)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        if (recheckInstruction?.Length > MaximumRecheckInstructionLength)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        return RecoveryPlanErrorCode.None;
    }

    public static RecoveryPlanErrorCode ValidatePhase(
        UpsertRecoveryPlanPhaseRequest request,
        string phaseName,
        string? instruction,
        int durationDays)
    {
        if (phaseName.Length is < 1 or > MaximumPhaseNameLength)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        if (request.StartDay < 1
            || request.EndDay < request.StartDay
            || request.EndDay > durationDays
            || request.SortOrder < 0)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        if (!IsValidDailyHours(request.SleepAndRestHoursPerDay))
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        if (instruction?.Length > MaximumPhaseInstructionLength)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        return RecoveryPlanErrorCode.None;
    }

    public static RecoveryPlanErrorCode ValidateNutrient(
        UpsertRecoveryPlanNutrientTargetRequest request,
        string nutrientName,
        string unit,
        string? instruction)
    {
        if (nutrientName.Length is < 1 or > MaximumNutrientNameLength
            || unit.Length is < 1 or > MaximumNutrientUnitLength
            || request.AmountPerDay <= 0
            || request.AmountPerDay > 99_999_999.99m
            || decimal.Round(request.AmountPerDay, 2) != request.AmountPerDay
            || request.SortOrder < 0)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        if (instruction?.Length > MaximumNutrientInstructionLength)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        return RecoveryPlanErrorCode.None;
    }

    public static RecoveryPlanErrorCode ValidateFood(
        UpsertRecoveryPlanFoodSourceRequest request,
        string foodName,
        string? suggestedServing,
        string? note)
    {
        if (foodName.Length is < 1 or > MaximumFoodNameLength
            || request.SortOrder < 0)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        if (suggestedServing?.Length > MaximumSuggestedServingLength
            || note?.Length > MaximumFoodNoteLength)
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        return RecoveryPlanErrorCode.None;
    }

    public static RecoveryPlanErrorCode ValidateCompletePlan(RecoveryPlan plan)
    {
        var planName = plan.PlanName?.Trim() ?? string.Empty;
        var summary = plan.Summary?.Trim() ?? string.Empty;
        var recheckInstruction = plan.RecheckInstruction?.Trim() ?? string.Empty;

        if (planName.Length is < 1 or > MaximumPlanNameLength
            || summary.Length is < 1 or > MaximumSummaryLength
            || recheckInstruction.Length is < 1 or > MaximumRecheckInstructionLength)
        {
            return RecoveryPlanErrorCode.RecoveryPlanIncomplete;
        }

        if (plan.DurationDays is < 1 or > 365)
        {
            return RecoveryPlanErrorCode.InvalidPlanStructure;
        }

        var phases = plan.Phases
            .Where(phase => !phase.IsDeleted)
            .OrderBy(phase => phase.StartDay)
            .ThenBy(phase => phase.EndDay)
            .ToList();

        if (phases.Count == 0)
        {
            return RecoveryPlanErrorCode.RecoveryPlanIncomplete;
        }

        if (phases.Select(phase => phase.SortOrder).Distinct().Count() != phases.Count)
        {
            return RecoveryPlanErrorCode.InvalidPlanStructure;
        }

        var expectedStart = 1;
        foreach (var phase in phases)
        {
            var phaseError = ValidateCompletePhase(phase, plan.DurationDays, expectedStart);
            if (phaseError != RecoveryPlanErrorCode.None)
            {
                return phaseError;
            }

            expectedStart = phase.EndDay + 1;
        }

        if (expectedStart != plan.DurationDays + 1)
        {
            return RecoveryPlanErrorCode.InvalidPlanStructure;
        }

        return RecoveryPlanErrorCode.None;
    }

    public static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static RecoveryPlanErrorCode ValidateCompletePhase(
        RecoveryPlanPhase phase,
        int durationDays,
        int expectedStart)
    {
        if (string.IsNullOrWhiteSpace(phase.PhaseName)
            || !phase.SleepAndRestHoursPerDay.HasValue)
        {
            return RecoveryPlanErrorCode.RecoveryPlanIncomplete;
        }

        if (phase.PhaseName.Trim().Length > MaximumPhaseNameLength
            || phase.Instruction?.Trim().Length > MaximumPhaseInstructionLength
            || phase.SortOrder < 0
            || phase.StartDay != expectedStart
            || phase.StartDay < 1
            || phase.EndDay < phase.StartDay
            || phase.EndDay > durationDays
            || !IsValidDailyHours(phase.SleepAndRestHoursPerDay))
        {
            return RecoveryPlanErrorCode.InvalidPlanStructure;
        }

        var nutrients = phase.NutrientTargets
            .Where(nutrient => !nutrient.IsDeleted)
            .ToList();

        if (nutrients.Count == 0)
        {
            return RecoveryPlanErrorCode.RecoveryPlanIncomplete;
        }

        if (nutrients.Select(nutrient => nutrient.SortOrder).Distinct().Count() != nutrients.Count)
        {
            return RecoveryPlanErrorCode.InvalidPlanStructure;
        }

        foreach (var nutrient in nutrients)
        {
            var nutrientError = ValidateCompleteNutrient(nutrient);
            if (nutrientError != RecoveryPlanErrorCode.None)
            {
                return nutrientError;
            }
        }

        return RecoveryPlanErrorCode.None;
    }

    private static RecoveryPlanErrorCode ValidateCompleteNutrient(
        RecoveryPlanNutrientTarget nutrient)
    {
        if (string.IsNullOrWhiteSpace(nutrient.NutrientName)
            || string.IsNullOrWhiteSpace(nutrient.Unit))
        {
            return RecoveryPlanErrorCode.RecoveryPlanIncomplete;
        }

        if (nutrient.NutrientName.Trim().Length > MaximumNutrientNameLength
            || nutrient.Unit.Trim().Length > MaximumNutrientUnitLength
            || nutrient.Instruction?.Trim().Length > MaximumNutrientInstructionLength
            || nutrient.AmountPerDay <= 0
            || nutrient.AmountPerDay > 99_999_999.99m
            || decimal.Round(nutrient.AmountPerDay, 2) != nutrient.AmountPerDay
            || nutrient.SortOrder < 0)
        {
            return RecoveryPlanErrorCode.InvalidPlanStructure;
        }

        var foods = nutrient.FoodSources
            .Where(food => !food.IsDeleted)
            .ToList();

        if (foods.Count == 0)
        {
            return RecoveryPlanErrorCode.RecoveryPlanIncomplete;
        }

        if (foods.Select(food => food.SortOrder).Distinct().Count() != foods.Count)
        {
            return RecoveryPlanErrorCode.InvalidPlanStructure;
        }

        if (foods.Any(food => string.IsNullOrWhiteSpace(food.FoodName)))
        {
            return RecoveryPlanErrorCode.RecoveryPlanIncomplete;
        }

        if (foods.Any(food =>
                food.FoodName.Trim().Length > MaximumFoodNameLength
                || food.SuggestedServing?.Trim().Length > MaximumSuggestedServingLength
                || food.Note?.Trim().Length > MaximumFoodNoteLength
                || food.SortOrder < 0))
        {
            return RecoveryPlanErrorCode.InvalidPlanStructure;
        }

        return RecoveryPlanErrorCode.None;
    }

    private static bool IsValidDailyHours(decimal? hours)
    {
        if (!hours.HasValue)
        {
            return true;
        }

        return hours.Value is >= 0 and <= 24
            && decimal.Round(hours.Value, 2) == hours.Value;
    }
}
