using MedMateAI.Application.Helpers;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Tests.Helpers;

[TestFixture]
public class LabTestIndicatorMatcherTests
{
    private static LabIndicatorMaster MakeIndicator(string? symbol, string? fullName, params string[] aliases)
    {
        var indicator = new LabIndicatorMaster
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            FullName = fullName,
            LabIndicatorAliases = aliases.Select((a, i) => new LabIndicatorAlias
            {
                Id = Guid.NewGuid(),
                AliasText = a,
                IsPrimary = i == 0
            }).ToList()
        };
        return indicator;
    }

    [Test]
    public void Match_NullInput_ReturnsNull()
    {
        var indicators = new List<LabIndicatorMaster> { MakeIndicator("HGB", "Hemoglobin") };
        var result = LabTestIndicatorMatcher.Match(null!, indicators);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Match_EmptyInput_ReturnsNull()
    {
        var indicators = new List<LabIndicatorMaster> { MakeIndicator("HGB", "Hemoglobin") };
        var result = LabTestIndicatorMatcher.Match("   ", indicators);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Match_EmptyIndicators_ReturnsNull()
    {
        var result = LabTestIndicatorMatcher.Match("HGB", new List<LabIndicatorMaster>());
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Match_ExactFullNameMatch_ReturnsConfidence1()
    {
        var indicator = MakeIndicator("HGB", "Hemoglobin");
        var result = LabTestIndicatorMatcher.Match("Hemoglobin", new[] { indicator });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Confidence, Is.EqualTo(1.0));
        Assert.That(result.Indicator, Is.EqualTo(indicator));
    }

    [Test]
    public void Match_ExactSymbolMatch_ReturnsConfidence095()
    {
        var indicator = MakeIndicator("HGB", "Hemoglobin");
        var result = LabTestIndicatorMatcher.Match("HGB", new[] { indicator });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Confidence, Is.EqualTo(0.95));
    }

    [Test]
    public void Match_CaseInsensitiveFullName_ReturnsMatch()
    {
        var indicator = MakeIndicator("HGB", "Hemoglobin");
        var result = LabTestIndicatorMatcher.Match("hemoglobin", new[] { indicator });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Confidence, Is.EqualTo(1.0));
    }

    [Test]
    public void Match_PartialContainsMatch_Returns085()
    {
        var indicator = MakeIndicator(null, "Glucose Fasting");
        var result = LabTestIndicatorMatcher.Match("Glucose", new[] { indicator });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Confidence, Is.EqualTo(0.85));
    }

    [Test]
    public void Match_PrimaryAliasExactMatch_ReturnsConfidence1()
    {
        var indicator = MakeIndicator("RBC", "Red Blood Cell Count", "Hồng cầu");
        var result = LabTestIndicatorMatcher.Match("Hồng cầu", new[] { indicator });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Confidence, Is.EqualTo(1.0)); // IsPrimary=true, exact → 1.0
    }

    [Test]
    public void Match_NoMatchingIndicator_ReturnsNull()
    {
        var indicator = MakeIndicator("HGB", "Hemoglobin");
        var result = LabTestIndicatorMatcher.Match("Cholesterol", new[] { indicator });
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Match_MultipleIndicators_ReturnsBestConfidence()
    {
        var ind1 = MakeIndicator(null, "Glucose Fasting");
        var ind2 = MakeIndicator("GLU", "Glucose");
        var result = LabTestIndicatorMatcher.Match("Glucose", new[] { ind1, ind2 });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Indicator, Is.EqualTo(ind2));
        Assert.That(result.Confidence, Is.EqualTo(1.0));
    }

    [Test]
    public void Match_InputWithTrailingPeriod_StillMatches()
    {
        var indicator = MakeIndicator("HGB", "Hemoglobin");
        var result = LabTestIndicatorMatcher.Match("Hemoglobin.", new[] { indicator });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Confidence, Is.EqualTo(1.0));
    }

    [Test]
    public void Match_AccentedOcrName_MatchesAsciiSymbol()
    {
        var indicator = MakeIndicator("ure", "Urea");
        var result = LabTestIndicatorMatcher.Match("Uré", new[] { indicator });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Confidence, Is.EqualTo(0.95));
        Assert.That(result.Indicator, Is.EqualTo(indicator));
    }

    [Test]
    public void Match_VietnameseAliasWithoutDiacritics_MatchesAccentedAlias()
    {
        var indicator = MakeIndicator("RBC", "Red Blood Cell Count", "Hồng cầu");
        var result = LabTestIndicatorMatcher.Match("hong cau", new[] { indicator });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Confidence, Is.EqualTo(1.0));
    }

    [Test]
    public void Match_MultipleWhitespaceBetweenTokens_StillMatches()
    {
        var indicator = MakeIndicator(null, "Hb A1c");
        var result = LabTestIndicatorMatcher.Match("Hb    A1c", new[] { indicator });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Confidence, Is.EqualTo(1.0));
    }

    [Test]
    public void Match_NonBreakingSpace_StillMatches()
    {
        var indicator = MakeIndicator(null, "Hb A1c");
        var result = LabTestIndicatorMatcher.Match("Hb\u00A0A1c", new[] { indicator });
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Confidence, Is.EqualTo(1.0));
    }
}
