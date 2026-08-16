using MedMateAI.Application.IRepository;
using MedMateAI.Application.Models.LabTests.Analytics;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class LabTestAnalyticsRepository : ILabTestAnalyticsRepository
{
    private readonly ApplicationDbContext _context;

    public LabTestAnalyticsRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LabTestTrendIndicatorData>>
        GetAvailableIndicatorsAsync(
            Guid userId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default)
    {
        var query = ApplyDateRange(BuildChartableMeasurements(userId), from, to);

        var rows = await query
            .Select(detail => new
            {
                IndicatorId = detail.IndicatorId!.Value,
                Symbol = detail.Indicator!.Symbol,
                Name = detail.Indicator.FullName,
                Unit = detail.Indicator.Unit,
                TestDate = detail.TestSession.TestDate!.Value
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => new
            {
                row.IndicatorId,
                row.Symbol,
                row.Name,
                row.Unit
            })
            .Select(group => new LabTestTrendIndicatorData(
                group.Key.IndicatorId,
                group.Key.Symbol,
                group.Key.Name,
                group.Key.Unit,
                group.Count(),
                group.Min(row => row.TestDate),
                group.Max(row => row.TestDate)))
            .OrderBy(indicator => indicator.Symbol)
            .ThenBy(indicator => indicator.IndicatorId)
            .ToList();
    }

    public async Task<IReadOnlyList<LabTestTrendMeasurementData>>
        GetIndicatorMeasurementsAsync(
            Guid userId,
            Guid indicatorId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default)
    {
        var query = ApplyDateRange(BuildChartableMeasurements(userId), from, to)
            .Where(detail => detail.IndicatorId == indicatorId);

        return await query
            .OrderBy(detail => detail.TestSession.TestDate)
            .ThenBy(detail => detail.TestSessionId)
            .ThenBy(detail => detail.Id)
            .Select(detail => new LabTestTrendMeasurementData(
                detail.Id,
                detail.TestSessionId,
                detail.IndicatorId!.Value,
                detail.Indicator!.Symbol,
                detail.Indicator.FullName,
                detail.Indicator.Unit,
                detail.TestSession.TestDate!.Value,
                detail.UserValue!.Value,
                detail.Status,
                detail.ReferenceMinUsed,
                detail.ReferenceMaxUsed,
                detail.ReferenceUnitUsed,
                detail.DeviationPercent,
                detail.TestSession.FacilityName))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<LabTestResultDetail> BuildChartableMeasurements(Guid userId)
    {
        return _context.LabTestResultDetails
            .AsNoTracking()
            .Where(detail =>
                !detail.IsDeleted
                && detail.IsMatched
                && detail.IndicatorId.HasValue
                && detail.UserValue.HasValue
                && detail.Indicator != null
                && !detail.Indicator.IsDeleted
                && !detail.TestSession.IsDeleted
                && detail.TestSession.UserId == userId
                && detail.TestSession.Status == LabTestSessionStatus.Completed
                && detail.TestSession.TestDate.HasValue);
    }

    private static IQueryable<LabTestResultDetail> ApplyDateRange(
        IQueryable<LabTestResultDetail> query,
        DateOnly? from,
        DateOnly? to)
    {
        if (from.HasValue)
        {
            query = query.Where(detail => detail.TestSession.TestDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(detail => detail.TestSession.TestDate <= to.Value);
        }

        return query;
    }
}
