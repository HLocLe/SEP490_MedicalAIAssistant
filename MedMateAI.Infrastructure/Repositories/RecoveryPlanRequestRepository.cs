using MedMateAI.Domain.Common;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using MedMateAI.Domain.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class RecoveryPlanRequestRepository : IRecoveryPlanRequestRepository
{
    private readonly ApplicationDbContext _context;
    public RecoveryPlanRequestRepository(ApplicationDbContext context) => _context = context;

    public Task<RecoveryPlanRequest?> GetByIdAsync(Guid id, bool tracked, CancellationToken token = default)
    {
        var query = _context.RecoveryPlanRequests.Where(x => x.Id == id && !x.IsDeleted);
        return (tracked ? query : query.AsNoTracking()).FirstOrDefaultAsync(token);
    }

    public async Task<RecoveryPlanRequest?> GetByIdForUpdateAsync(Guid id, CancellationToken token = default)
    {
        RequireTransaction();
        var rows = await _context.RecoveryPlanRequests
            .FromSqlInterpolated($"""SELECT * FROM "RecoveryPlanRequest" WHERE "RecoveryPlanRequestId"={id} AND "IsDeleted"=false FOR UPDATE""")
            .AsTracking().ToListAsync(token);
        return rows.FirstOrDefault();
    }

    public Task<PagedResult<RecoveryPlanRequest>> GetOpenPagedAsync(int page, int size, RecoveryPlanDiseaseGroup? group, CancellationToken token = default) =>
        PageAsync(_context.RecoveryPlanRequests.AsNoTracking().Where(x => !x.IsDeleted
            && x.Status == RecoveryPlanRequestStatus.WaitingForDoctor && x.AssignedDoctorId == null
            && (!group.HasValue || x.DiseaseGroup == group)), page, size, token);

    public Task<PagedResult<RecoveryPlanRequest>> GetByUserPagedAsync(Guid userId, int page, int size, RecoveryPlanRequestStatus? status, CancellationToken token = default) =>
        PageAsync(_context.RecoveryPlanRequests.AsNoTracking().Where(x => !x.IsDeleted && x.UserId == userId
            && (!status.HasValue || x.Status == status)), page, size, token);

    public Task<PagedResult<RecoveryPlanRequest>> GetAssignedPagedAsync(Guid doctorId, int page, int size, RecoveryPlanRequestStatus? status, CancellationToken token = default) =>
        PageAsync(_context.RecoveryPlanRequests.AsNoTracking().Where(x => !x.IsDeleted && x.AssignedDoctorId == doctorId
            && (!status.HasValue || x.Status == status)), page, size, token);

    public async Task<RecoveryPlanRequest?> TryAcceptAsync(Guid id, Guid doctorId, DateTime now, DateTime expiresAt, CancellationToken token = default)
    {
        RequireTransaction();
        const string sql = """
            UPDATE "RecoveryPlanRequest"
            SET "AssignedDoctorId"=@doctorId, "Status"='Assigned', "AcceptedAt"=@now,
                "AssignmentExpiresAt"=@expiresAt, "UpdatedAt"=@now, "Version"="Version"+1
            WHERE "RecoveryPlanRequestId"=@requestId AND "IsDeleted"=false
              AND "Status"='WaitingForDoctor' AND "AssignedDoctorId" IS NULL
            RETURNING "RecoveryPlanRequestId";
            """;
        var transaction = _context.Database.CurrentTransaction!;
        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction.GetDbTransaction();
        AddParameter(command, "requestId", id);
        AddParameter(command, "doctorId", doctorId);
        AddParameter(command, "now", now);
        AddParameter(command, "expiresAt", expiresAt);
        var acceptedId = await command.ExecuteScalarAsync(token);
        return acceptedId is Guid ? await GetByIdAsync(id, true, token) : null;
    }

    public Task<Doctor?> GetDoctorByUserIdAsync(Guid userId, CancellationToken token = default) =>
        _context.Doctors.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, token);

    public async Task<Doctor?> GetDoctorByUserIdForUpdateAsync(Guid userId, CancellationToken token = default)
    {
        RequireTransaction();
        var rows = await _context.Doctors.FromSqlInterpolated(
            $"""SELECT * FROM "Doctor" WHERE "UserId"={userId} AND "IsDeleted"=false FOR UPDATE""")
            .AsTracking().ToListAsync(token);
        return rows.FirstOrDefault();
    }

    public Task<int> CountActiveAssignmentsAsync(Guid doctorId, CancellationToken token = default) =>
        _context.RecoveryPlanRequests.CountAsync(x => !x.IsDeleted && x.AssignedDoctorId == doctorId
            && (x.Status == RecoveryPlanRequestStatus.Assigned || x.Status == RecoveryPlanRequestStatus.InReview
                || x.Status == RecoveryPlanRequestStatus.NeedMoreInformation), token);

    public Task<bool> IsOwnedTreatmentJourneyAsync(Guid id, Guid userId, CancellationToken token = default) =>
        _context.TreatmentJourneys.AnyAsync(x => x.Id == id && x.UserId == userId && !x.IsDeleted, token);
    public Task<bool> IsOwnedLabSessionAsync(Guid id, Guid userId, CancellationToken token = default) =>
        _context.LabTestSessions.AnyAsync(x => x.Id == id && x.UserId == userId && !x.IsDeleted, token);
    public void Add(RecoveryPlanRequest request) => _context.RecoveryPlanRequests.Add(request);
    public void AddEvent(RecoveryPlanRequestEvent requestEvent) => _context.RecoveryPlanRequestEvents.Add(requestEvent);
    public void AddOutbox(OutboxMessage message) => _context.OutboxMessages.Add(message);

    private void RequireTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Request row locking requires an active database transaction.");
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task<PagedResult<RecoveryPlanRequest>> PageAsync(
        IQueryable<RecoveryPlanRequest> query, int page, int size, CancellationToken token)
    {
        var count = await query.CountAsync(token);
        var items = await query.OrderBy(x => x.RequestedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(token);
        return new PagedResult<RecoveryPlanRequest>
        {
            Items = items, TotalCount = count, PageNumber = page, PageSize = size
        };
    }
}
