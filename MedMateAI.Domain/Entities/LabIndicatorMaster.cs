namespace MedMateAI.Domain.Entities;

public sealed class LabIndicatorMaster : BaseEntity
{
    public string Symbol { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? Unit { get; set; }

    public double? MinReference { get; set; }

    public double? MaxReference { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<LabIndicatorAlias> LabIndicatorAliases { get; set; } = new List<LabIndicatorAlias>();

    public ICollection<LabIndicatorReferenceRange> LabIndicatorReferenceRanges { get; set; } =
        new List<LabIndicatorReferenceRange>();

    public ICollection<LabTestResultDetail> LabTestResultDetails { get; set; } = new List<LabTestResultDetail>();

    public ICollection<LabIndicatorAdviceCache> LabIndicatorAdviceCaches { get; set; } = new List<LabIndicatorAdviceCache>();
}
