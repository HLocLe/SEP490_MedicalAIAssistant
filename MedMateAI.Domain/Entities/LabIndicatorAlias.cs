namespace MedMateAI.Domain.Entities;

public sealed class LabIndicatorAlias : BaseEntity
{
    public Guid IndicatorId { get; set; }

    public string AliasText { get; set; } = string.Empty;

    public string? Language { get; set; }

    public bool IsPrimary { get; set; }

    public LabIndicatorMaster Indicator { get; set; } = null!;
}
