using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.LabIndicators.Requests;

public sealed class UpdateLabIndicatorReferenceRangeRequest
{
    public Gender? Gender { get; set; }

    public AgeGroup? AgeGroup { get; set; }

    public ReferenceComparisonType ComparisonType { get; set; } = ReferenceComparisonType.Between;

    public double? MinValue { get; set; }

    public double? MaxValue { get; set; }

    public string? Unit { get; set; }
}
