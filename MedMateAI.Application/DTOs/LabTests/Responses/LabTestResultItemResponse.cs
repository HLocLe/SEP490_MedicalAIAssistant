using MedMateAI.Application.DTOs.LabIndicators.Responses;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.LabTests.Responses;

public sealed class LabTestResultItemResponse
{
    public Guid? ResultDetailId { get; set; }

    public string RawExtractedName { get; set; } = string.Empty;

    public string? RawExtractedValue { get; set; }

    public double? UserValue { get; set; }

    public LabResultStatus Status { get; set; }

    public bool IsMatched { get; set; }

    public double? MatchConfidence { get; set; }

    public double? ReferenceMinUsed { get; set; }

    public double? ReferenceMaxUsed { get; set; }

    public string? ReferenceUnitUsed { get; set; }

    public ReferenceComparisonType? ComparisonTypeUsed { get; set; }

    public double? DeviationPercent { get; set; }

    public LabIndicatorResponse? Indicator { get; set; }

    public LabIndicatorReferenceRangeResponse? ReferenceRangeUsed { get; set; }

    public LabIndicatorAdviceCacheResponse? Advice { get; set; }
}
