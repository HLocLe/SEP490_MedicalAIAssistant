using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class LabIndicatorReferenceRange : BaseEntity
{
    public Guid IndicatorId { get; set; }

    public Gender? Gender { get; set; }

    public AgeGroup? AgeGroup { get; set; }

    public ReferenceComparisonType ComparisonType { get; set; } = ReferenceComparisonType.Between;

    public double? MinValue { get; set; }

    public double? MaxValue { get; set; }

    public string? Unit { get; set; }

    public int Priority { get; set; }

    public LabIndicatorMaster Indicator { get; set; } = null!;
}
