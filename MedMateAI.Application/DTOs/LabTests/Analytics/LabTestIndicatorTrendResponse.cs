using MedMateAI.Application.Models.LabTests.Analytics;

namespace MedMateAI.Application.DTOs.LabTests.Analytics;

public sealed class LabTestIndicatorTrendResponse
{
    public Guid IndicatorId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int MeasurementCount { get; set; }
    public double? LatestValue { get; set; }
    public double? PreviousValue { get; set; }
    public LabTestTrendClassification Trend { get; set; }
    public bool HasMixedUnits { get; set; }
    public IReadOnlyList<LabTestTrendPointResponse> Points { get; set; } =
        Array.Empty<LabTestTrendPointResponse>();
}
