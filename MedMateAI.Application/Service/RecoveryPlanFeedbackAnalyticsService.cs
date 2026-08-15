using System.Globalization;
using MedMateAI.Application.DTOs.RecoveryPlans.Analytics;
using MedMateAI.Application.IRepository;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Analytics;

namespace MedMateAI.Application.Service;

public sealed class RecoveryPlanFeedbackAnalyticsService
    : IRecoveryPlanFeedbackAnalyticsService
{
    private readonly IRecoveryPlanFeedbackAnalyticsRepository _repository;

    public RecoveryPlanFeedbackAnalyticsService(
        IRecoveryPlanFeedbackAnalyticsRepository repository)
    {
        _repository = repository;
    }

    public async Task<AnalyticsOperationResult<RecoveryPlanFeedbackAnalyticsResponse>>
        GetAsync(
            Guid doctorUserId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default)
    {
        if (doctorUserId == Guid.Empty)
        {
            return AnalyticsOperationResult<RecoveryPlanFeedbackAnalyticsResponse>.Fail(
                AnalyticsErrorCode.InvalidRequest,
                "Doctor user ID is invalid.");
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return AnalyticsOperationResult<RecoveryPlanFeedbackAnalyticsResponse>.Fail(
                AnalyticsErrorCode.InvalidDateRange,
                "The from date must be on or before the to date.");
        }

        var analytics = await _repository.GetAnalyticsAsync(
            doctorUserId,
            from,
            to,
            cancellationToken);
        if (analytics is null)
        {
            return AnalyticsOperationResult<RecoveryPlanFeedbackAnalyticsResponse>.Fail(
                AnalyticsErrorCode.DoctorProfileNotFound,
                "Doctor profile was not found.");
        }

        var validFeedbacks = analytics.Feedbacks
            .Where(feedback => feedback.Rating is >= 1 and <= 5)
            .ToList();
        var totalFeedbacks = analytics.Feedbacks.Count;

        var distribution = Enumerable.Range(1, 5)
            .Select(rating => new RecoveryPlanRatingDistributionResponse
            {
                Rating = rating,
                Count = validFeedbacks.Count(feedback => feedback.Rating == rating)
            })
            .ToList();

        var timeline = validFeedbacks
            .GroupBy(feedback => new
            {
                feedback.SubmittedAt.Year,
                feedback.SubmittedAt.Month
            })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .Select(group => new RecoveryPlanFeedbackTimelineResponse
            {
                Period = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{group.Key.Year:D4}-{group.Key.Month:D2}"),
                AverageRating = RoundToTwoDecimals(group.Average(feedback => feedback.Rating)),
                FeedbackCount = group.Count()
            })
            .ToList();

        var response = new RecoveryPlanFeedbackAnalyticsResponse
        {
            AverageRating = validFeedbacks.Count == 0
                ? 0
                : RoundToTwoDecimals(validFeedbacks.Average(feedback => feedback.Rating)),
            TotalFeedbacks = totalFeedbacks,
            CompletedPlans = analytics.CompletedPlans,
            FeedbackRate = analytics.CompletedPlans == 0
                ? 0
                : RoundToTwoDecimals(
                    totalFeedbacks * 100d / analytics.CompletedPlans),
            RatingDistribution = distribution,
            Timeline = timeline
        };

        return AnalyticsOperationResult<RecoveryPlanFeedbackAnalyticsResponse>.Ok(response);
    }

    private static double RoundToTwoDecimals(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
