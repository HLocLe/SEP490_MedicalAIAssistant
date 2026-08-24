using MedMateAI.Application.DTOs.Common;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Service;

internal static class RecoveryPlanMapping
{
    public static RecoveryPlanSummaryResponse ToSummary(RecoveryPlan plan)
    {
        var requestId = GetRequiredRequestId(plan);

        return new RecoveryPlanSummaryResponse
        {
            Id = plan.Id,
            RecoveryPlanRequestId = requestId,
            PlanName = plan.PlanName,
            DurationDays = plan.DurationDays,
            Status = plan.Status,
            PublishedAt = plan.PublishedAt,
            ActivatedAt = plan.ActivatedAt,
            CancelledAt = plan.CancelledAt,
            FeedbackRating = plan.FeedbackRating,
            FeedbackNote = plan.FeedbackNote,
            FeedbackSubmittedAt = plan.FeedbackSubmittedAt,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            IsCurrent = plan.IsCurrent
        };
    }

    public static RecoveryPlanDetailResponse ToDetail(RecoveryPlan plan)
    {
        var requestId = GetRequiredRequestId(plan);

        return new RecoveryPlanDetailResponse
        {
            Id = plan.Id,
            RecoveryPlanRequestId = requestId,
            PlanName = plan.PlanName,
            Summary = plan.Summary,
            DurationDays = plan.DurationDays,
            Status = plan.Status,
            PublishedAt = plan.PublishedAt,
            ActivatedAt = plan.ActivatedAt,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            CompletedAt = plan.CompletedAt,
            FeedbackRating = plan.FeedbackRating,
            FeedbackNote = plan.FeedbackNote,
            FeedbackSubmittedAt = plan.FeedbackSubmittedAt,
            CancelledAt = plan.CancelledAt,
            CancellationReasonCode = plan.CancellationReasonCode,
            CancellationReason = plan.CancellationReason,
            RecheckInstruction = plan.RecheckInstruction,
            IsCurrent = plan.IsCurrent,
            Phases = plan.Phases
                .Where(phase => !phase.IsDeleted)
                .OrderBy(phase => phase.StartDay)
                .ThenBy(phase => phase.SortOrder)
                .ThenBy(phase => phase.Id)
                .Select(ToPhase)
                .ToList()
        };
    }

    public static DoctorRecoveryPlanDetailResponse ToDoctorDetail(
        RecoveryPlan plan,
        RecoveryPlanClinicalSnapshot? snapshot)
    {
        var requestId = GetRequiredRequestId(plan);

        return new DoctorRecoveryPlanDetailResponse
        {
            Plan = ToDetail(plan),
            RequestId = requestId,
            DiseaseGroup = plan.RecoveryPlanRequest!.DiseaseGroup,
            DoctorId = plan.DoctorId,
            ClinicalSnapshot = ToClinicalSnapshotResponse(snapshot)
        };
    }

    private static RecoveryPlanClinicalSnapshotResponse? ToClinicalSnapshotResponse(
        RecoveryPlanClinicalSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        return new RecoveryPlanClinicalSnapshotResponse
        {
            SchemaVersion = snapshot.SchemaVersion,
            CapturedAtUtc = snapshot.CapturedAtUtc,
            RequestId = snapshot.RequestId,
            DiseaseGroup = snapshot.DiseaseGroup,
            PatientProfile = ToSnapshotPatientProfileResponse(snapshot.PatientProfile),
            ChronicDiseases = (snapshot.ChronicDiseases
                    ?? Array.Empty<RecoveryPlanSnapshotChronicDisease>())
                .Select(ToSnapshotChronicDiseaseResponse)
                .ToList(),
            PrimaryLabTest = ToSnapshotPrimaryLabTestResponse(snapshot.PrimaryLabTest),
            UserMedications = (snapshot.UserMedications
                    ?? Array.Empty<RecoveryPlanSnapshotMedication>())
                .Select(ToSnapshotMedicationResponse)
                .ToList(),
            TreatmentJourney = ToSnapshotTreatmentJourneyResponse(
                snapshot.TreatmentJourney)
        };
    }

    private static RecoveryPlanSnapshotPatientProfileResponse?
        ToSnapshotPatientProfileResponse(
            RecoveryPlanSnapshotPatientProfile? patientProfile)
    {
        if (patientProfile is null)
        {
            return null;
        }

        return new RecoveryPlanSnapshotPatientProfileResponse
        {
            HeightCm = patientProfile.HeightCm,
            WeightKg = patientProfile.WeightKg,
            Bmi = patientProfile.Bmi,
            AllergyNote = patientProfile.AllergyNote,
            ProfileCreatedAtUtc = patientProfile.ProfileCreatedAtUtc,
            ProfileUpdatedAtUtc = patientProfile.ProfileUpdatedAtUtc
        };
    }

    private static RecoveryPlanSnapshotChronicDiseaseResponse
        ToSnapshotChronicDiseaseResponse(
            RecoveryPlanSnapshotChronicDisease chronicDisease)
    {
        return new RecoveryPlanSnapshotChronicDiseaseResponse
        {
            DiseaseName = chronicDisease.DiseaseName,
            From = chronicDisease.From,
            To = chronicDisease.To,
            Note = chronicDisease.Note
        };
    }

    private static RecoveryPlanSnapshotPrimaryLabTestResponse?
        ToSnapshotPrimaryLabTestResponse(
            RecoveryPlanSnapshotPrimaryLabTest? primaryLabTest)
    {
        if (primaryLabTest is null)
        {
            return null;
        }

        return new RecoveryPlanSnapshotPrimaryLabTestResponse
        {
            TestSessionId = primaryLabTest.TestSessionId,
            FacilityName = primaryLabTest.FacilityName,
            CreatedAtUtc = primaryLabTest.CreatedAtUtc,
            UpdatedAtUtc = primaryLabTest.UpdatedAtUtc,
            Results = (primaryLabTest.Results
                    ?? Array.Empty<RecoveryPlanSnapshotLabResult>())
                .Select(ToSnapshotLabResultResponse)
                .ToList()
        };
    }

    private static RecoveryPlanSnapshotLabResultResponse ToSnapshotLabResultResponse(
        RecoveryPlanSnapshotLabResult labResult)
    {
        return new RecoveryPlanSnapshotLabResultResponse
        {
            IndicatorId = labResult.IndicatorId,
            Symbol = labResult.Symbol,
            FullName = labResult.FullName,
            Unit = labResult.Unit,
            UserValue = labResult.UserValue,
            Status = labResult.Status,
            MinReference = labResult.MinReference,
            MaxReference = labResult.MaxReference
        };
    }

    private static RecoveryPlanSnapshotMedicationResponse ToSnapshotMedicationResponse(
        RecoveryPlanSnapshotMedication medication)
    {
        return new RecoveryPlanSnapshotMedicationResponse
        {
            UserMedicationId = medication.UserMedicationId,
            MedicineName = medication.MedicineName,
            DosageInstruction = medication.DosageInstruction,
            StartDate = medication.StartDate,
            EndDate = medication.EndDate,
            Status = medication.Status,
            SourceType = medication.SourceType
        };
    }

    private static RecoveryPlanSnapshotTreatmentJourneyResponse?
        ToSnapshotTreatmentJourneyResponse(
            RecoveryPlanSnapshotTreatmentJourney? treatmentJourney)
    {
        if (treatmentJourney is null)
        {
            return null;
        }

        return new RecoveryPlanSnapshotTreatmentJourneyResponse
        {
            Id = treatmentJourney.Id,
            Title = treatmentJourney.Title,
            DiagnosisSummary = treatmentJourney.DiagnosisSummary,
            StartDate = treatmentJourney.StartDate,
            EndDate = treatmentJourney.EndDate,
            Status = treatmentJourney.Status,
            ApprovedAt = treatmentJourney.ApprovedAt,
            ApprovalStatus = treatmentJourney.ApprovalStatus,
            ApprovalNote = treatmentJourney.ApprovalNote
        };
    }

    public static RecoveryPlanPhaseResponse ToPhase(RecoveryPlanPhase phase)
    {
        return new RecoveryPlanPhaseResponse
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

    public static RecoveryPlanNutrientTargetResponse ToNutrient(
        RecoveryPlanNutrientTarget nutrient)
    {
        return new RecoveryPlanNutrientTargetResponse
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

    public static RecoveryPlanFoodSourceResponse ToFood(RecoveryPlanFoodSource food)
    {
        return new RecoveryPlanFoodSourceResponse
        {
            Id = food.Id,
            FoodName = food.FoodName,
            SuggestedServing = food.SuggestedServing,
            Note = food.Note,
            SortOrder = food.SortOrder
        };
    }

    public static PagedResponse<RecoveryPlanSummaryResponse> ToPage(
        PagedResult<RecoveryPlan> page)
    {
        return new PagedResponse<RecoveryPlanSummaryResponse>
        {
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
            Items = page.Items.Select(ToSummary).ToList()
        };
    }

    private static Guid GetRequiredRequestId(RecoveryPlan plan)
    {
        if (!plan.RecoveryPlanRequestId.HasValue)
        {
            throw new InvalidOperationException(
                "Phase 3 recovery plan mapping requires a recovery plan request.");
        }

        return plan.RecoveryPlanRequestId.Value;
    }
}
