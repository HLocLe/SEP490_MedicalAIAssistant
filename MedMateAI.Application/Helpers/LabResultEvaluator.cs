using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Helpers;

public static class LabResultEvaluator
{
    private const double CriticalDeviationPercent = 50;

    public static LabResultStatus Evaluate(
        double userValue,
        ReferenceComparisonType comparisonType,
        double? minValue,
        double? maxValue)
    {
        return comparisonType switch
        {
            ReferenceComparisonType.LessThanOrEqual when maxValue.HasValue =>
                EvaluateUpperBound(userValue, maxValue.Value),

            ReferenceComparisonType.GreaterThanOrEqual when minValue.HasValue =>
                EvaluateLowerBound(userValue, minValue.Value),

            ReferenceComparisonType.Between when minValue.HasValue && maxValue.HasValue =>
                EvaluateRange(userValue, minValue.Value, maxValue.Value),

            _ => LabResultStatus.Unknown,
        };
    }

    public static LabResultStatus Evaluate(
        double userValue,
        LabIndicatorReferenceRange referenceRange)
    {
        return Evaluate(
            userValue,
            referenceRange.ComparisonType,
            referenceRange.MinValue,
            referenceRange.MaxValue);
    }

    public static LabResultStatus Evaluate(
        double userValue,
        LabIndicatorMaster indicator,
        Gender? gender = null,
        AgeGroup? ageGroup = null)
    {
        var referenceRange = SelectReferenceRange(indicator.LabIndicatorReferenceRanges, gender, ageGroup);
        if (referenceRange is not null)
        {
            return Evaluate(userValue, referenceRange);
        }

        if (indicator.MinReference.HasValue && indicator.MaxReference.HasValue)
        {
            return EvaluateRange(userValue, indicator.MinReference.Value, indicator.MaxReference.Value);
        }

        return LabResultStatus.Unknown;
    }

    public static LabIndicatorReferenceRange? SelectReferenceRange(
        IEnumerable<LabIndicatorReferenceRange> ranges,
        Gender? gender,
        AgeGroup? ageGroup)
    {
        return ranges
            .Where(r => !r.IsDeleted)
            .Where(r => r.Gender is null || r.Gender == gender)
            .Where(r => r.AgeGroup is null || r.AgeGroup == ageGroup)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => (r.Gender.HasValue ? 1 : 0) + (r.AgeGroup.HasValue ? 1 : 0))
            .FirstOrDefault();
    }

    public static double? CalculateDeviationPercent(
        double userValue,
        ReferenceComparisonType comparisonType,
        double? minValue,
        double? maxValue)
    {
        return comparisonType switch
        {
            ReferenceComparisonType.Between when minValue.HasValue && maxValue.HasValue && maxValue.Value > minValue.Value =>
                userValue < minValue.Value
                    ? (minValue.Value - userValue) / minValue.Value * 100
                    : userValue > maxValue.Value
                        ? (userValue - maxValue.Value) / maxValue.Value * 100
                        : 0,

            ReferenceComparisonType.LessThanOrEqual when maxValue.HasValue && maxValue.Value > 0 && userValue > maxValue.Value =>
                (userValue - maxValue.Value) / maxValue.Value * 100,

            ReferenceComparisonType.GreaterThanOrEqual when minValue.HasValue && minValue.Value > 0 && userValue < minValue.Value =>
                (minValue.Value - userValue) / minValue.Value * 100,

            _ => null,
        };
    }

    private static LabResultStatus EvaluateRange(double userValue, double minValue, double maxValue)
    {
        if (userValue >= minValue && userValue <= maxValue)
        {
            return LabResultStatus.Normal;
        }

        if (userValue > maxValue)
        {
            var deviation = CalculateDeviationPercent(
                userValue,
                ReferenceComparisonType.Between,
                minValue,
                maxValue);

            return deviation >= CriticalDeviationPercent
                ? LabResultStatus.CriticalHigh
                : LabResultStatus.High;
        }

        var lowDeviation = CalculateDeviationPercent(
            userValue,
            ReferenceComparisonType.Between,
            minValue,
            maxValue);

        return lowDeviation >= CriticalDeviationPercent
            ? LabResultStatus.CriticalLow
            : LabResultStatus.Low;
    }

    private static LabResultStatus EvaluateUpperBound(double userValue, double maxValue)
    {
        if (userValue <= maxValue)
        {
            return LabResultStatus.Normal;
        }

        var deviation = CalculateDeviationPercent(
            userValue,
            ReferenceComparisonType.LessThanOrEqual,
            null,
            maxValue);

        return deviation >= CriticalDeviationPercent
            ? LabResultStatus.CriticalHigh
            : LabResultStatus.High;
    }

    private static LabResultStatus EvaluateLowerBound(double userValue, double minValue)
    {
        if (userValue >= minValue)
        {
            return LabResultStatus.Normal;
        }

        var deviation = CalculateDeviationPercent(
            userValue,
            ReferenceComparisonType.GreaterThanOrEqual,
            minValue,
            null);

        return deviation >= CriticalDeviationPercent
            ? LabResultStatus.CriticalLow
            : LabResultStatus.Low;
    }
}
