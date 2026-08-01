using MedMateAI.Domain.Enums;

namespace MedMateAI.Domain.Entities;

public sealed class LabTestResultDetail : BaseEntity
{
    public Guid TestSessionId { get; set; }

    public Guid? IndicatorId { get; set; }

    public string? RawExtractedName { get; set; }

    public string? RawExtractedValue { get; set; }

    public double? UserValue { get; set; }

    public LabResultStatus Status { get; set; } = LabResultStatus.Unknown;

    public bool IsMatched { get; set; }

    public double? MatchConfidence { get; set; }

    public double? ReferenceMinUsed { get; set; }

    public double? ReferenceMaxUsed { get; set; }

    public string? ReferenceUnitUsed { get; set; }

    public double? DeviationPercent { get; set; }

    public Guid? AdviceCacheId { get; set; }

    public LabTestSession TestSession { get; set; } = null!;

    public LabIndicatorMaster? Indicator { get; set; }

    public LabIndicatorAdviceCache? AdviceCache { get; set; }
}
