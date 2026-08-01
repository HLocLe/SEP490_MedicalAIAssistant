namespace MedMateAI.Domain.Entities;

public sealed class LabTestOcrExtract : BaseEntity
{
    public Guid TestSessionId { get; set; }

    public int RowIndex { get; set; }

    public string? ExtractedTestName { get; set; }

    public string? ExtractedValue { get; set; }

    public string? ExtractedUnit { get; set; }

    public string? ExtractedReferenceText { get; set; }

    public LabTestSession TestSession { get; set; } = null!;
}
