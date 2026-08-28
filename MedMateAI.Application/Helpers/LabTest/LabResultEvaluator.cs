using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.Helpers.LabTest;

public static class LabResultEvaluator
{
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
        return referenceRange is null
            ? LabResultStatus.Unknown
            : Evaluate(userValue, referenceRange);
    }

   
    public static LabIndicatorReferenceRange? SelectReferenceRange(
        IEnumerable<LabIndicatorReferenceRange> ranges,
        Gender? gender,
        AgeGroup? ageGroup)
    {
        var active = ranges.Where(r => !r.IsDeleted).ToList();
        if (active.Count == 0)
        {
            return null;
        }

        if (gender.HasValue)
        {
            var byGender = active.FirstOrDefault(r => r.Gender == gender);
            if (byGender is not null)
            {
                return byGender;
            }
        }

        if (ageGroup.HasValue)
        {
            var byAge = active.FirstOrDefault(r => r.AgeGroup == ageGroup);
            if (byAge is not null)
            {
                return byAge;
            }
        }

        return active.FirstOrDefault(r => r.Gender is null && r.AgeGroup is null);
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

        return userValue > maxValue
            ? LabResultStatus.High
            : LabResultStatus.Low;
    }

    private static LabResultStatus EvaluateUpperBound(double userValue, double maxValue)
    {
        return userValue <= maxValue
            ? LabResultStatus.Normal
            : LabResultStatus.High;
    }

    private static LabResultStatus EvaluateLowerBound(double userValue, double minValue)
    {
        return userValue >= minValue
            ? LabResultStatus.Normal
            : LabResultStatus.Low;
    }
}
