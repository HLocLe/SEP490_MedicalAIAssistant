using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Domain.Repository;

public interface IPaymentRepository : IGenericRepository<Payment>
{
    Task<PagedResult<Payment>> GetPagedWithSubscriptionAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Payment>> GetPagedByUserIdWithSubscriptionAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Payment?> GetByIdWithSubscriptionAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
