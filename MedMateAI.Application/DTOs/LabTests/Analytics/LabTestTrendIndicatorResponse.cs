namespace MedMateAI.Application.DTOs.LabTests.Analytics;

public sealed class LabTestTrendIndicatorResponse
{
    public Guid IndicatorId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int MeasurementCount { get; set; }
    public DateOnly FirstTestDate { get; set; }
    public DateOnly LatestTestDate { get; set; }
}
