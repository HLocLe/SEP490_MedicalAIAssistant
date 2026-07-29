using System.Text.Json;
using System.Text.Json.Serialization;
using MedMateAI.Application.DTOs.RecoveryPlans;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models;
using MedMateAI.Application.Models.RecoveryPlans;
using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Persistence;

namespace MedMateAI.Application.Service;

public sealed class RecoveryPlanClinicalContextService : IRecoveryPlanClinicalContextService
{
    private const int ClinicalSnapshotSchemaVersion = 1;

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IUnitOfWork _unitOfWork;

    public RecoveryPlanClinicalContextService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<RecoveryPlanOperationResult<RecoveryPlanClinicalContextResponse>>
        GetForDoctorAsync(
            Guid doctorUserId,
            Guid requestId,
            CancellationToken cancellationToken)
    {
        var doctor = await _unitOfWork.RecoveryPlanRequests.GetDoctorByUserIdAsync(
            doctorUserId,
            cancellationToken);

        if (doctor is null || doctor.IsDeleted)
        {
            return RecoveryPlanOperationResult<RecoveryPlanClinicalContextResponse>.Fail(
                RecoveryPlanErrorCode.DoctorProfileNotFound);
        }

        if (!doctor.IsActive)
        {
            return RecoveryPlanOperationResult<RecoveryPlanClinicalContextResponse>.Fail(
                RecoveryPlanErrorCode.DoctorNotActive);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Keep assignment authorization stable until the clinical projection is complete.
            var recoveryPlanRequest =
                await _unitOfWork.RecoveryPlanRequests.GetByIdForUpdateAsync(
                    requestId,
                    cancellationToken);

            if (recoveryPlanRequest is null
                || recoveryPlanRequest.IsDeleted
                || recoveryPlanRequest.AssignedDoctorId != doctor.Id)
            {
                return await RollbackFailureAsync(
                    RecoveryPlanErrorCode.NotFound);
            }

            var accessError = ValidateClinicalContextAccess(
                recoveryPlanRequest,
                DateTime.UtcNow);
            if (accessError != RecoveryPlanErrorCode.None)
            {
                return await RollbackFailureAsync(accessError);
            }

            var context = await _unitOfWork.RecoveryPlans.GetClinicalContextAsync(
                requestId,
                cancellationToken);
            if (context is null
                || context.AssignedDoctorId != doctor.Id
                || context.RequestStatus != recoveryPlanRequest.Status)
            {
                return await RollbackFailureAsync(
                    RecoveryPlanErrorCode.Conflict);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return RecoveryPlanOperationResult<RecoveryPlanClinicalContextResponse>.Ok(
                MapContext(context));
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public async Task<RecoveryPlanClinicalSnapshot?> BuildSnapshotAsync(
        Guid requestId,
        DateTime capturedAtUtc,
        CancellationToken cancellationToken)
    {
        var context = await _unitOfWork.RecoveryPlans.GetClinicalContextAsync(
            requestId,
            cancellationToken);

        if (context is null)
        {
            return null;
        }

        return new RecoveryPlanClinicalSnapshot
        {
            SchemaVersion = ClinicalSnapshotSchemaVersion,
            CapturedAtUtc = capturedAtUtc,
            RequestId = context.RequestId,
            DiseaseGroup = context.DiseaseGroup,
            PatientProfile = MapSnapshotProfile(context.PatientProfile),
            ChronicDiseases = context.ChronicDiseases
                .Select(MapSnapshotChronicDisease)
                .ToList(),
            PrimaryLabTest = MapSnapshotLabTest(context.PrimaryLabTest),
            UserMedications = context.UserMedications
                .Select(MapSnapshotMedication)
                .ToList(),
            TreatmentJourney = MapSnapshotTreatmentJourney(context.TreatmentJourney)
        };
    }

    public string SerializeSnapshot(RecoveryPlanClinicalSnapshot snapshot)
    {
        return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
    }

    public RecoveryPlanClinicalSnapshot? DeserializeSnapshot(string? snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RecoveryPlanClinicalSnapshot>(
            snapshotJson,
            SnapshotJsonOptions);
    }

    private static RecoveryPlanClinicalContextResponse MapContext(
        RecoveryPlanClinicalContextData context)
    {
        return new RecoveryPlanClinicalContextResponse
        {
            RequestId = context.RequestId,
            DiseaseGroup = context.DiseaseGroup,
            TreatmentJourneyId = context.TreatmentJourneyId,
            PrimaryLabTestSessionId = context.PrimaryLabTestSessionId,
            RequestedAt = context.RequestedAt,
            RequestNote = context.RequestNote,
            PatientProfile = MapProfile(context.PatientProfile),
            ChronicDiseases = context.ChronicDiseases.Select(MapChronicDisease).ToList(),
            PrimaryLabTest = MapLabTest(context.PrimaryLabTest),
            UserMedications = context.UserMedications.Select(MapMedication).ToList(),
            TreatmentJourney = MapTreatmentJourney(context.TreatmentJourney)
        };
    }

    private static RecoveryPlanPatientProfileResponse? MapProfile(
        RecoveryPlanPatientProfileData? profile)
    {
        if (profile is null)
        {
            return null;
        }

        return new RecoveryPlanPatientProfileResponse
        {
            Height = profile.HeightCm,
            Weight = profile.WeightKg,
            Bmi = CalculateBmi(profile.HeightCm, profile.WeightKg),
            AllergyNote = profile.AllergyNote,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        };
    }

    private static RecoveryPlanSnapshotPatientProfile? MapSnapshotProfile(
        RecoveryPlanPatientProfileData? profile)
    {
        if (profile is null)
        {
            return null;
        }

        return new RecoveryPlanSnapshotPatientProfile
        {
            HeightCm = profile.HeightCm,
            WeightKg = profile.WeightKg,
            Bmi = CalculateBmi(profile.HeightCm, profile.WeightKg),
            AllergyNote = profile.AllergyNote,
            ProfileCreatedAtUtc = profile.CreatedAt,
            ProfileUpdatedAtUtc = profile.UpdatedAt
        };
    }

    private static RecoveryPlanChronicDiseaseResponse MapChronicDisease(
        RecoveryPlanChronicDiseaseData disease)
    {
        return new RecoveryPlanChronicDiseaseResponse
        {
            DiseaseName = disease.DiseaseName,
            From = disease.From,
            To = disease.To,
            Note = disease.Note
        };
    }

    private static RecoveryPlanSnapshotChronicDisease MapSnapshotChronicDisease(
        RecoveryPlanChronicDiseaseData disease)
    {
        return new RecoveryPlanSnapshotChronicDisease
        {
            DiseaseName = disease.DiseaseName,
            From = disease.From,
            To = disease.To,
            Note = disease.Note
        };
    }

    private static RecoveryPlanLabTestResponse? MapLabTest(RecoveryPlanLabTestData? labTest)
    {
        if (labTest is null)
        {
            return null;
        }

        return new RecoveryPlanLabTestResponse
        {
            TestSessionId = labTest.TestSessionId,
            CreatedAt = labTest.CreatedAt,
            UpdatedAt = labTest.UpdatedAt,
            Results = labTest.Results.Select(MapLabResult).ToList()
        };
    }

    private static RecoveryPlanSnapshotPrimaryLabTest? MapSnapshotLabTest(
        RecoveryPlanLabTestData? labTest)
    {
        if (labTest is null)
        {
            return null;
        }

        return new RecoveryPlanSnapshotPrimaryLabTest
        {
            TestSessionId = labTest.TestSessionId,
            CreatedAtUtc = labTest.CreatedAt,
            UpdatedAtUtc = labTest.UpdatedAt,
            Results = labTest.Results.Select(MapSnapshotLabResult).ToList()
        };
    }

    private static RecoveryPlanLabResultResponse MapLabResult(
        RecoveryPlanLabResultData result)
    {
        return new RecoveryPlanLabResultResponse
        {
            IndicatorId = result.IndicatorId,
            Symbol = result.Symbol,
            FullName = result.FullName,
            Unit = result.Unit,
            UserValue = result.UserValue,
            Status = result.Status,
            MinReference = result.MinReference,
            MaxReference = result.MaxReference
        };
    }

    private static RecoveryPlanSnapshotLabResult MapSnapshotLabResult(
        RecoveryPlanLabResultData result)
    {
        return new RecoveryPlanSnapshotLabResult
        {
            IndicatorId = result.IndicatorId,
            Symbol = result.Symbol,
            FullName = result.FullName,
            Unit = result.Unit,
            UserValue = result.UserValue,
            Status = result.Status,
            MinReference = result.MinReference,
            MaxReference = result.MaxReference
        };
    }

    private static RecoveryPlanUserMedicationResponse MapMedication(
        RecoveryPlanUserMedicationData medication)
    {
        return new RecoveryPlanUserMedicationResponse
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

    private static RecoveryPlanSnapshotMedication MapSnapshotMedication(
        RecoveryPlanUserMedicationData medication)
    {
        return new RecoveryPlanSnapshotMedication
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

    private static RecoveryPlanTreatmentJourneyResponse? MapTreatmentJourney(
        RecoveryPlanTreatmentJourneyData? journey)
    {
        if (journey is null)
        {
            return null;
        }

        return new RecoveryPlanTreatmentJourneyResponse
        {
            Id = journey.Id,
            Title = journey.Title,
            DiagnosisSummary = journey.DiagnosisSummary,
            StartDate = journey.StartDate,
            EndDate = journey.EndDate,
            Status = journey.Status,
            ApprovedAt = journey.ApprovedAt,
            ApprovalStatus = journey.ApprovalStatus,
            ApprovalNote = journey.ApprovalNote
        };
    }

    private static RecoveryPlanSnapshotTreatmentJourney? MapSnapshotTreatmentJourney(
        RecoveryPlanTreatmentJourneyData? journey)
    {
        if (journey is null)
        {
            return null;
        }

        return new RecoveryPlanSnapshotTreatmentJourney
        {
            Id = journey.Id,
            Title = journey.Title,
            DiagnosisSummary = journey.DiagnosisSummary,
            StartDate = journey.StartDate,
            EndDate = journey.EndDate,
            Status = journey.Status,
            ApprovedAt = journey.ApprovedAt,
            ApprovalStatus = journey.ApprovalStatus,
            ApprovalNote = journey.ApprovalNote
        };
    }

    private static double? CalculateBmi(double? heightCm, double? weightKg)
    {
        if (!heightCm.HasValue
            || !weightKg.HasValue
            || heightCm.Value <= 0
            || weightKg.Value <= 0)
        {
            return null;
        }

        var heightMeters = heightCm.Value / 100;
        return Math.Round(
            weightKg.Value / (heightMeters * heightMeters),
            2,
            MidpointRounding.AwayFromZero);
    }

    private static RecoveryPlanErrorCode ValidateClinicalContextAccess(
        RecoveryPlanRequest request,
        DateTime utcNow)
    {
        if (request.Status == RecoveryPlanRequestStatus.Assigned)
        {
            if (!request.AssignmentExpiresAt.HasValue
                || request.AssignmentExpiresAt.Value <= utcNow)
            {
                return RecoveryPlanErrorCode.AssignmentExpired;
            }

            return RecoveryPlanErrorCode.None;
        }

        if (request.Status is RecoveryPlanRequestStatus.InReview
            or RecoveryPlanRequestStatus.NeedMoreInformation)
        {
            return RecoveryPlanErrorCode.None;
        }

        return RecoveryPlanErrorCode.InvalidRequestState;
    }

    private async Task<RecoveryPlanOperationResult<RecoveryPlanClinicalContextResponse>>
        RollbackFailureAsync(RecoveryPlanErrorCode error)
    {
        await RollbackAsync();
        return RecoveryPlanOperationResult<RecoveryPlanClinicalContextResponse>.Fail(error);
    }

    private Task RollbackAsync()
    {
        return _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
    }
}
