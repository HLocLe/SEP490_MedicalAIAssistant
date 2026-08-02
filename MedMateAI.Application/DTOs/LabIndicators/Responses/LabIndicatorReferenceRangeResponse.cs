using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.LabIndicators.Responses;

public sealed class LabIndicatorReferenceRangeResponse
{
    public Guid ReferenceRangeId { get; set; }

    public Guid IndicatorId { get; set; }

    public Gender? Gender { get; set; }

    public AgeGroup? AgeGroup { get; set; }

    public ReferenceComparisonType ComparisonType { get; set; }

    public double? MinValue { get; set; }

    public double? MaxValue { get; set; }

    public string? Unit { get; set; }
}
