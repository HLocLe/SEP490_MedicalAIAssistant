namespace MedMateAI.Application.DTOs.LabTests.Responses;

public sealed class LabTestOcrExtractResponse
{
    public Guid OcrExtractId { get; set; }

    public Guid TestSessionId { get; set; }

    public int RowIndex { get; set; }

    public string? ExtractedTestName { get; set; }

    public string? ExtractedValue { get; set; }

    public string? ExtractedUnit { get; set; }

    public string? ExtractedReferenceText { get; set; }

    public DateTime CreatedAt { get; set; }
}
