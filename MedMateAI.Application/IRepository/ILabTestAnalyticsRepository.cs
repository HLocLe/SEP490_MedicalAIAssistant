using MedMateAI.Application.Models.LabTests.Analytics;

namespace MedMateAI.Application.IRepository;

public interface ILabTestAnalyticsRepository
{
    Task<IReadOnlyList<LabTestTrendIndicatorData>> GetAvailableIndicatorsAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LabTestTrendMeasurementData>> GetIndicatorMeasurementsAsync(
        Guid userId,
        Guid indicatorId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}
