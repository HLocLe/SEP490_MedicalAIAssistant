using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlanTemplates;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Service;

internal static class RecoveryPlanTemplateMapping
{
    public static RecoveryPlanTemplateSummaryResponse ToSummary(
        RecoveryPlanTemplate template)
    {
        return new RecoveryPlanTemplateSummaryResponse
        {
            Id = template.Id,
            TemplateName = template.TemplateName,
            DiseaseGroup = template.DiseaseGroup,
            PlanName = template.PlanName,
            DurationDays = template.DurationDays,
            IsComplete = RecoveryPlanTemplateValidation.IsComplete(template),
            PhaseCount = template.Phases.Count(phase => !phase.IsDeleted),
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    public static RecoveryPlanTemplateDetailResponse ToDetail(RecoveryPlanTemplate template)
    {
        return new RecoveryPlanTemplateDetailResponse
        {
            Id = template.Id,
            TemplateName = template.TemplateName,
            DiseaseGroup = template.DiseaseGroup,
            PlanName = template.PlanName,
            DurationDays = template.DurationDays,
            Summary = template.Summary,
            RecheckInstruction = template.RecheckInstruction,
            IsComplete = RecoveryPlanTemplateValidation.IsComplete(template),
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
            Phases = template.Phases
                .Where(phase => !phase.IsDeleted)
                .OrderBy(phase => phase.SortOrder)
                .ThenBy(phase => phase.Id)
                .Select(ToPhase)
                .ToList()
        };
    }

    public static RecoveryPlanTemplatePhaseResponse ToPhase(
        RecoveryPlanTemplatePhase phase)
    {
        return new RecoveryPlanTemplatePhaseResponse
        {
            Id = phase.Id,
            PhaseName = phase.PhaseName,
            StartDay = phase.StartDay,
            EndDay = phase.EndDay,
            SleepAndRestHoursPerDay = phase.SleepAndRestHoursPerDay,
            Instruction = phase.Instruction,
            SortOrder = phase.SortOrder,
            NutrientTargets = phase.NutrientTargets
                .Where(nutrient => !nutrient.IsDeleted)
                .OrderBy(nutrient => nutrient.SortOrder)
                .ThenBy(nutrient => nutrient.Id)
                .Select(ToNutrient)
                .ToList()
        };
    }

    public static RecoveryPlanTemplateNutrientTargetResponse ToNutrient(
        RecoveryPlanTemplateNutrientTarget nutrient)
    {
        return new RecoveryPlanTemplateNutrientTargetResponse
        {
            Id = nutrient.Id,
            NutrientName = nutrient.NutrientName,
            AmountPerDay = nutrient.AmountPerDay,
            Unit = nutrient.Unit,
            Instruction = nutrient.Instruction,
            SortOrder = nutrient.SortOrder,
            FoodSources = nutrient.FoodSources
                .Where(food => !food.IsDeleted)
                .OrderBy(food => food.SortOrder)
                .ThenBy(food => food.Id)
                .Select(ToFood)
                .ToList()
        };
    }

    public static RecoveryPlanTemplateFoodSourceResponse ToFood(
        RecoveryPlanTemplateFoodSource food)
    {
        return new RecoveryPlanTemplateFoodSourceResponse
        {
            Id = food.Id,
            FoodName = food.FoodName,
            SuggestedServing = food.SuggestedServing,
            Note = food.Note,
            SortOrder = food.SortOrder
        };
    }

    public static PagedResponse<RecoveryPlanTemplateSummaryResponse> ToPage(
        PagedResult<RecoveryPlanTemplate> page)
    {
        return PagedResponse<RecoveryPlanTemplateSummaryResponse>.From(page, ToSummary);
    }

    public static RecoveryPlan ToDraft(
        RecoveryPlanTemplate template,
        RecoveryPlanRequest request,
        DateTime utcNow)
    {
        var plan = new RecoveryPlan
        {
            Id = Guid.NewGuid(),
            RecoveryPlanRequestId = request.Id,
            UserId = request.UserId,
            DoctorId = request.AssignedDoctorId,
            TreatmentJourneyId = request.TreatmentJourneyId,
            TestSessionId = request.PrimaryLabTestSessionId,
            SymptomAnalysisSessionId = null,
            PlanName = template.PlanName,
            Summary = template.Summary,
            DurationDays = template.DurationDays,
            Status = RecoveryPlanStatus.Draft,
            StartDate = null,
            EndDate = null,
            PublishedAt = null,
            ActivatedAt = null,
            CompletedAt = null,
            FeedbackRating = null,
            FeedbackNote = null,
            FeedbackSubmittedAt = null,
            CancelledAt = null,
            CancelledByUserId = null,
            CancellationReasonCode = null,
            CancellationReason = null,
            ClinicalSnapshotJson = null,
            RecheckInstruction = template.RecheckInstruction,
            IsCurrent = false,
            CreatedAt = utcNow,
            UpdatedAt = null
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
                SortOrder = templatePhase.SortOrder,
                CreatedAt = utcNow
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
                    SortOrder = templateNutrient.SortOrder,
                    CreatedAt = utcNow
                };

                foreach (var templateFood in templateNutrient.FoodSources.Where(
                             food => !food.IsDeleted))
                {
                    nutrient.FoodSources.Add(new RecoveryPlanFoodSource
                    {
                        FoodName = templateFood.FoodName,
                        SuggestedServing = templateFood.SuggestedServing,
                        Note = templateFood.Note,
                        SortOrder = templateFood.SortOrder,
                        CreatedAt = utcNow
                    });
                }

                phase.NutrientTargets.Add(nutrient);
            }

            plan.Phases.Add(phase);
        }

        return plan;
    }
}
