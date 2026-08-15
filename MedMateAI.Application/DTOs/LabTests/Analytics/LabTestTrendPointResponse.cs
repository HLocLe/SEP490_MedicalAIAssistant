using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.LabTests.Analytics;

public sealed class LabTestTrendPointResponse
{
    public Guid SessionId { get; set; }
    public DateOnly TestDate { get; set; }
    public double Value { get; set; }
    public LabResultStatus Status { get; set; }
    public double? ReferenceMin { get; set; }
    public double? ReferenceMax { get; set; }
    public string? Unit { get; set; }
    public double? DeviationPercent { get; set; }
    public string? FacilityName { get; set; }
}
