using MedMateAI.Application.IRepository;
using MedMateAI.Application.Models.RecoveryPlans.Analytics;
using MedMateAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedMateAI.Infrastructure.Repositories;

public sealed class RecoveryPlanFeedbackAnalyticsRepository
    : IRecoveryPlanFeedbackAnalyticsRepository
{
    private readonly ApplicationDbContext _context;

    public RecoveryPlanFeedbackAnalyticsRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecoveryPlanFeedbackAnalyticsData?> GetAnalyticsAsync(
        Guid doctorUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var doctorId = await _context.Doctors
            .AsNoTracking()
            .Where(doctor =>
                doctor.UserId == doctorUserId
                && !doctor.IsDeleted)
            .Select(doctor => (Guid?)doctor.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!doctorId.HasValue)
        {
            return null;
        }

        var cohort = _context.RecoveryPlans
            .AsNoTracking()
            .Where(plan =>
                !plan.IsDeleted
                && plan.DoctorId == doctorId.Value
                && plan.Status == RecoveryPlanStatus.Completed
                && plan.CompletedAt.HasValue);

        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(
                from.Value.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Utc);
            cohort = cohort.Where(plan => plan.CompletedAt >= fromUtc);
        }

        if (to.HasValue && to.Value != DateOnly.MaxValue)
        {
            var toExclusiveUtc = DateTime.SpecifyKind(
                to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Utc);
            cohort = cohort.Where(plan => plan.CompletedAt < toExclusiveUtc);
        }

        var completedPlans = await cohort.CountAsync(cancellationToken);
        var feedbacks = await cohort
            .Where(plan =>
                plan.FeedbackRating.HasValue
                && plan.FeedbackSubmittedAt.HasValue)
            .Select(plan => new RecoveryPlanFeedbackData(
                plan.FeedbackRating!.Value,
                plan.FeedbackSubmittedAt!.Value))
            .ToListAsync(cancellationToken);

        return new RecoveryPlanFeedbackAnalyticsData(completedPlans, feedbacks);
    }
}
