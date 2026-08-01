using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.LabTests.Responses;

public sealed class LabTestSessionSummaryResponse
{
    public Guid SessionId { get; set; }

    public string? DocumentUrl { get; set; }

    public LabTestSessionStatus Status { get; set; }

    public DateOnly? TestDate { get; set; }

    public Gender? PatientGenderAtTest { get; set; }

    public int? PatientAgeAtTest { get; set; }

    public string? FacilityName { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
