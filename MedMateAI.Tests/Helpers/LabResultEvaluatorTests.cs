using MedMateAI.Application.Helpers.LabTest;
using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;

namespace MedMateAI.Tests.Helpers;

[TestFixture]
public class LabResultEvaluatorTests
{
    // ──────────────────────────────────────────────
    // Evaluate(double, ReferenceComparisonType, double?, double?)
    // ──────────────────────────────────────────────

    [Test]
    public void Evaluate_Between_ValueInRange_ReturnsNormal()
    {
        var result = LabResultEvaluator.Evaluate(7.5, ReferenceComparisonType.Between, 5.0, 10.0);
        Assert.That(result, Is.EqualTo(LabResultStatus.Normal));
    }

    [Test]
    public void Evaluate_Between_ValueAtMin_ReturnsNormal()
    {
        var result = LabResultEvaluator.Evaluate(5.0, ReferenceComparisonType.Between, 5.0, 10.0);
        Assert.That(result, Is.EqualTo(LabResultStatus.Normal));
    }

    [Test]
    public void Evaluate_Between_ValueAtMax_ReturnsNormal()
    {
        var result = LabResultEvaluator.Evaluate(10.0, ReferenceComparisonType.Between, 5.0, 10.0);
        Assert.That(result, Is.EqualTo(LabResultStatus.Normal));
    }

    [Test]
    public void Evaluate_Between_ValueBelowMin_ReturnsLow()
    {
        var result = LabResultEvaluator.Evaluate(4.9, ReferenceComparisonType.Between, 5.0, 10.0);
        Assert.That(result, Is.EqualTo(LabResultStatus.Low));
    }

    [Test]
    public void Evaluate_Between_ValueAboveMax_ReturnsHigh()
    {
        var result = LabResultEvaluator.Evaluate(10.1, ReferenceComparisonType.Between, 5.0, 10.0);
        Assert.That(result, Is.EqualTo(LabResultStatus.High));
    }

    [Test]
    public void Evaluate_LessThanOrEqual_ValueAtMax_ReturnsNormal()
    {
        var result = LabResultEvaluator.Evaluate(10.0, ReferenceComparisonType.LessThanOrEqual, null, 10.0);
        Assert.That(result, Is.EqualTo(LabResultStatus.Normal));
    }

    [Test]
    public void Evaluate_LessThanOrEqual_ValueAboveMax_ReturnsHigh()
    {
        var result = LabResultEvaluator.Evaluate(10.1, ReferenceComparisonType.LessThanOrEqual, null, 10.0);
        Assert.That(result, Is.EqualTo(LabResultStatus.High));
    }

    [Test]
    public void Evaluate_GreaterThanOrEqual_ValueAtMin_ReturnsNormal()
    {
        var result = LabResultEvaluator.Evaluate(5.0, ReferenceComparisonType.GreaterThanOrEqual, 5.0, null);
        Assert.That(result, Is.EqualTo(LabResultStatus.Normal));
    }

    [Test]
    public void Evaluate_GreaterThanOrEqual_ValueBelowMin_ReturnsLow()
    {
        var result = LabResultEvaluator.Evaluate(4.9, ReferenceComparisonType.GreaterThanOrEqual, 5.0, null);
        Assert.That(result, Is.EqualTo(LabResultStatus.Low));
    }

    [Test]
    public void Evaluate_UnknownComparisonType_ReturnsUnknown()
    {
        var result = LabResultEvaluator.Evaluate(7.0, (ReferenceComparisonType)99, 5.0, 10.0);
        Assert.That(result, Is.EqualTo(LabResultStatus.Unknown));
    }

    // ──────────────────────────────────────────────
    // SelectReferenceRange
    // ──────────────────────────────────────────────

    [Test]
    public void SelectReferenceRange_EmptyList_ReturnsNull()
    {
        var result = LabResultEvaluator.SelectReferenceRange([], null, null);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void SelectReferenceRange_GenderMatch_ReturnsByGender()
    {
        var ranges = new List<LabIndicatorReferenceRange>
        {
            new() { Id = Guid.NewGuid(), Gender = Gender.Male, MinValue = 13, MaxValue = 17, ComparisonType = ReferenceComparisonType.Between },
            new() { Id = Guid.NewGuid(), Gender = null, MinValue = 12, MaxValue = 16, ComparisonType = ReferenceComparisonType.Between }
        };

        var result = LabResultEvaluator.SelectReferenceRange(ranges, Gender.Male, null);
        Assert.That(result!.Gender, Is.EqualTo(Gender.Male));
    }

    [Test]
    public void SelectReferenceRange_NoGenderMatch_FallsBackToGeneric()
    {
        var generic = new LabIndicatorReferenceRange
        {
            Id = Guid.NewGuid(), Gender = null, AgeGroup = null,
            MinValue = 12, MaxValue = 16, ComparisonType = ReferenceComparisonType.Between
        };
        var ranges = new List<LabIndicatorReferenceRange> { generic };

        var result = LabResultEvaluator.SelectReferenceRange(ranges, Gender.Female, null);
        Assert.That(result, Is.EqualTo(generic));
    }

    [Test]
    public void SelectReferenceRange_DeletedRangesIgnored_ReturnsNull()
    {
        var ranges = new List<LabIndicatorReferenceRange>
        {
            new() { Id = Guid.NewGuid(), IsDeleted = true, MinValue = 5, MaxValue = 10, ComparisonType = ReferenceComparisonType.Between }
        };

        var result = LabResultEvaluator.SelectReferenceRange(ranges, null, null);
        Assert.That(result, Is.Null);
    }

    // ──────────────────────────────────────────────
    // CalculateDeviationPercent
    // ──────────────────────────────────────────────

    [Test]
    public void CalculateDeviationPercent_Between_ValueInRange_ReturnsZero()
    {
        var result = LabResultEvaluator.CalculateDeviationPercent(7.5, ReferenceComparisonType.Between, 5.0, 10.0);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void CalculateDeviationPercent_Between_ValueBelowMin_ReturnsPositivePercent()
    {
        var result = LabResultEvaluator.CalculateDeviationPercent(4.0, ReferenceComparisonType.Between, 5.0, 10.0);
        Assert.That(result, Is.EqualTo(20.0).Within(0.001));
    }

    [Test]
    public void CalculateDeviationPercent_Between_ValueAboveMax_ReturnsPositivePercent()
    {
        var result = LabResultEvaluator.CalculateDeviationPercent(12.0, ReferenceComparisonType.Between, 5.0, 10.0);
        Assert.That(result, Is.EqualTo(20.0).Within(0.001));
    }

    [Test]
    public void CalculateDeviationPercent_LessThanOrEqual_ValueAboveMax_ReturnsPercent()
    {
        var result = LabResultEvaluator.CalculateDeviationPercent(12.0, ReferenceComparisonType.LessThanOrEqual, null, 10.0);
        Assert.That(result, Is.EqualTo(20.0).Within(0.001));
    }

    [Test]
    public void CalculateDeviationPercent_GreaterThanOrEqual_ValueBelowMin_ReturnsPercent()
    {
        var result = LabResultEvaluator.CalculateDeviationPercent(4.0, ReferenceComparisonType.GreaterThanOrEqual, 5.0, null);
        Assert.That(result, Is.EqualTo(20.0).Within(0.001));
    }

    [Test]
    public void CalculateDeviationPercent_UnmatchedCase_ReturnsNull()
    {
        var result = LabResultEvaluator.CalculateDeviationPercent(7.0, (ReferenceComparisonType)99, null, null);
        Assert.That(result, Is.Null);
    }
}
