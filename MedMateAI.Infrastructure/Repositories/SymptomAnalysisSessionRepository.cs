using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class SymptomAnalysisSessionRepository
    : GenericRepository<SymptomAnalysisSession>, ISymptomAnalysisSessionRepository
{
    public SymptomAnalysisSessionRepository(ApplicationDbContext context)
        : base(context)
    {
    }

    public Task<PagedResult<SymptomAnalysisSession>> GetPagedByUserIdAsync(
        Guid userId,
        SymptomAnalysisSessionType? sessionType,
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
                && (!sessionType.HasValue || session.SessionType == sessionType.Value),
            query => query
                .OrderByDescending(session => session.CreatedAt)
                .ThenByDescending(session => session.Id),
            asNoTracking: true,
            cancellationToken);
    }

    public Task<PagedResult<SymptomAnalysisSession>> GetPagedAllAsync(
        SymptomAnalysisSessionType? sessionType,
        SymptomAnalysisSessionStatus? status,
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
                && (!sessionType.HasValue || session.SessionType == sessionType.Value)
                && (!status.HasValue || session.Status == status.Value)
                && (!userId.HasValue || session.UserId == userId.Value),
            query => query
                .OrderByDescending(session => session.CreatedAt)
                .ThenByDescending(session => session.Id),
            asNoTracking: true,
            cancellationToken);
    }
}
