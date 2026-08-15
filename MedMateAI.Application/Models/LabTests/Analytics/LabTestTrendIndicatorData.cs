namespace MedMateAI.Application.Models.LabTests.Analytics;

public sealed record LabTestTrendIndicatorData(
    Guid IndicatorId,
    string Symbol,
    string? Name,
    string? Unit,
    int MeasurementCount,
    DateOnly FirstTestDate,
    DateOnly LatestTestDate);
