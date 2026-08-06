using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class LabTestSessionRepository
    : GenericRepository<LabTestSession>, ILabTestSessionRepository
{
    private readonly ApplicationDbContext _context;

    public LabTestSessionRepository(ApplicationDbContext context)
        : base(context)
    {
        _context = context;
    }

    public async Task<LabTestSession?> GetByIdWithResultsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.LabTestSessions
            .AsNoTracking()
            .Include(x => x.LabTestResultDetails.Where(d => !d.IsDeleted))
                .ThenInclude(d => d.Indicator)
            .Include(x => x.LabTestResultDetails.Where(d => !d.IsDeleted))
                .ThenInclude(d => d.AdviceCache)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    public Task<PagedResult<LabTestSession>> GetPagedByUserIdAsync(
        Guid userId,
        LabTestSessionStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return GetPagedAsync(
            pageNumber,
            pageSize,
            session =>
                !session.IsDeleted
                && session.UserId == userId
                && (!status.HasValue || session.Status == status.Value),
            query => query
                .OrderByDescending(session => session.CreatedAt)
                .ThenByDescending(session => session.Id),
            asNoTracking: true,
            cancellationToken);
    }

    public Task<PagedResult<LabTestSession>> GetPagedAllAsync(
        LabTestSessionStatus? status,
        Guid? userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (userId.HasValue && userId.Value == Guid.Empty)
        {
            return GetPagedAsync(
                pageNumber,
                pageSize,
                session => false,
                cancellationToken: cancellationToken);
        }

        return GetPagedAsync(
            pageNumber,
            pageSize,
            session =>
                !session.IsDeleted
                && (!status.HasValue || session.Status == status.Value)
                && (!userId.HasValue || session.UserId == userId.Value),
            query => query
                .OrderByDescending(session => session.CreatedAt)
                .ThenByDescending(session => session.Id),
            asNoTracking: true,
            cancellationToken);
    }
}
