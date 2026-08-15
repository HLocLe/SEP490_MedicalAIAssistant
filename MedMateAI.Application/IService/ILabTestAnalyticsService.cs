using MedMateAI.Application.DTOs.LabTests.Analytics;
using MedMateAI.Application.Models.Analytics;

namespace MedMateAI.Application.IService;

public interface ILabTestAnalyticsService
{
    Task<AnalyticsOperationResult<IReadOnlyList<LabTestTrendIndicatorResponse>>>
        GetAvailableIndicatorsAsync(
            Guid userId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default);

    Task<AnalyticsOperationResult<LabTestIndicatorTrendResponse>> GetIndicatorTrendAsync(
        Guid userId,
        Guid indicatorId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}
