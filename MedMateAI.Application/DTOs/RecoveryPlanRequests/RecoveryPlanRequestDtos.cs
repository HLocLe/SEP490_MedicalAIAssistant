using System.ComponentModel.DataAnnotations;
using MedMateAI.Application.DTOs.Common;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.RecoveryPlanRequests;

public sealed class CreateRecoveryPlanRequest
{
    [EnumDataType(typeof(RecoveryPlanDiseaseGroup))]
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public Guid? TreatmentJourneyId { get; set; }
    public Guid? PrimaryLabTestSessionId { get; set; }
    [MaxLength(2000)] public string? RequestNote { get; set; }
}
public sealed class ProvideMoreInformationRequest { [Required, MaxLength(2000)] public string AdditionalInformation { get; set; } = string.Empty; }
public sealed class ReleaseRecoveryPlanRequest { [MaxLength(2000)] public string? Reason { get; set; } }
public sealed class RequestMoreInformationRequest { [Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty; }
public sealed class RejectRecoveryPlanRequest
{
    [Required, MaxLength(100)] public string RejectionReasonCode { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string RejectionReason { get; set; } = string.Empty;
}
public sealed class RecoveryPlanRequestResponse
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? AssignedDoctorId { get; set; }
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public Guid? TreatmentJourneyId { get; set; }
    public Guid? PrimaryLabTestSessionId { get; set; }
    public RecoveryPlanRequestStatus Status { get; set; }
    public string? RequestNote { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? ReviewStartedAt { get; set; }
    public DateTime? AssignmentExpiresAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? RejectionReasonCode { get; set; }
    public string? RejectionReason { get; set; }
    public int Version { get; set; }
}
public sealed class OpenRecoveryPlanRequestResponse
{
    public Guid Id { get; set; }
    public RecoveryPlanDiseaseGroup DiseaseGroup { get; set; }
    public RecoveryPlanRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
}
public sealed class SubscriptionUsageResponse
{
    public string QuotaCode { get; set; } = string.Empty;
    public string QuotaName { get; set; } = string.Empty;
    public int LimitValue { get; set; }
    public int UsedCount { get; set; }
    public int ReservedCount { get; set; }
    public int RemainingCount => Math.Max(0, LimitValue - UsedCount - ReservedCount);
    public DateTime CycleStart { get; set; }
    public DateTime CycleEnd { get; set; }
    public QuotaResetPeriod ResetPeriod { get; set; }
}
