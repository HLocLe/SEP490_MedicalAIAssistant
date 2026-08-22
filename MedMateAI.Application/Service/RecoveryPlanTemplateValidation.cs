using MedMateAI.Application.DTOs.RecoveryPlanTemplates;
using MedMateAI.Application.Models;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Service;

internal static class RecoveryPlanTemplateValidation
{
    public const int MaximumTemplateNameLength = 200;

    public static RecoveryPlanErrorCode ValidateHeader(
        string templateName,
        CreateRecoveryPlanTemplateRequest request,
        string planName,
        string? summary,
        string? recheckInstruction)
    {
        return ValidateHeader(
            templateName,
            request.DiseaseGroup,
            planName,
            request.DurationDays,
            summary,
            recheckInstruction);
    }

    public static RecoveryPlanErrorCode ValidateHeader(
        string templateName,
        UpdateRecoveryPlanTemplateRequest request,
        string planName,
        string? summary,
        string? recheckInstruction)
    {
        return ValidateHeader(
            templateName,
            request.DiseaseGroup,
            planName,
            request.DurationDays,
            summary,
            recheckInstruction);
    }

    public static bool IsComplete(RecoveryPlanTemplate template)
    {
        if (template.TemplateName.Trim().Length is < 1 or > MaximumTemplateNameLength
            || !Enum.IsDefined(template.DiseaseGroup))
        {
            return false;
        }

        var validationPlan = new RecoveryPlan
        {
            PlanName = template.PlanName,
            Summary = template.Summary,
            DurationDays = template.DurationDays,
            RecheckInstruction = template.RecheckInstruction
        };

        foreach (var templatePhase in template.Phases.Where(phase => !phase.IsDeleted))
        {
            var phase = new RecoveryPlanPhase
            {
                PhaseName = templatePhase.PhaseName,
                StartDay = templatePhase.StartDay,
                EndDay = templatePhase.EndDay,
                SleepAndRestHoursPerDay = templatePhase.SleepAndRestHoursPerDay,
                Instruction = templatePhase.Instruction,
                SortOrder = templatePhase.SortOrder
            };

            foreach (var templateNutrient in templatePhase.NutrientTargets.Where(
                         nutrient => !nutrient.IsDeleted))
            {
                var nutrient = new RecoveryPlanNutrientTarget
                {
                    NutrientName = templateNutrient.NutrientName,
                    AmountPerDay = templateNutrient.AmountPerDay,
                    Unit = templateNutrient.Unit,
                    Instruction = templateNutrient.Instruction,
                    SortOrder = templateNutrient.SortOrder
                };

                foreach (var templateFood in templateNutrient.FoodSources.Where(
                             food => !food.IsDeleted))
                {
                    nutrient.FoodSources.Add(new RecoveryPlanFoodSource
                    {
                        FoodName = templateFood.FoodName,
                        SuggestedServing = templateFood.SuggestedServing,
                        Note = templateFood.Note,
                        SortOrder = templateFood.SortOrder
                    });
                }

                phase.NutrientTargets.Add(nutrient);
            }

            validationPlan.Phases.Add(phase);
        }

        return RecoveryPlanValidation.ValidateCompletePlan(validationPlan)
            == RecoveryPlanErrorCode.None;
    }

    private static RecoveryPlanErrorCode ValidateHeader(
        string templateName,
        RecoveryPlanDiseaseGroup diseaseGroup,
        string planName,
        int durationDays,
        string? summary,
        string? recheckInstruction)
    {
        if (templateName.Length is < 1 or > MaximumTemplateNameLength
            || !Enum.IsDefined(diseaseGroup))
        {
            return RecoveryPlanErrorCode.InvalidRequest;
        }

        return RecoveryPlanValidation.ValidateDraftHeader(
            planName,
            summary,
            durationDays,
            recheckInstruction);
    }
}
