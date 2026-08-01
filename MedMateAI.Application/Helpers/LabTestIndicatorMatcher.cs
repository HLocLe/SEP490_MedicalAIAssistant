using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Helpers;

public sealed record IndicatorMatchResult(
    LabIndicatorMaster Indicator,
    double Confidence,
    string MatchedText);

public static class LabTestIndicatorMatcher
{
    public static IndicatorMatchResult? Match(
        string testName,
        IReadOnlyList<LabIndicatorMaster> indicators)
    {
        if (string.IsNullOrWhiteSpace(testName) || indicators.Count == 0)
        {
            return null;
        }

        var normalizedInput = Normalize(testName);
        IndicatorMatchResult? bestMatch = null;

        foreach (var indicator in indicators)
        {
            foreach (var candidate in GetMatchCandidates(indicator))
            {
                var normalizedCandidate = Normalize(candidate.Text);
                if (string.IsNullOrEmpty(normalizedCandidate))
                {
                    continue;
                }

                double confidence;
                if (string.Equals(normalizedInput, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
                {
                    confidence = candidate.IsPrimary ? 1.0 : 0.95;
                }
                else if (normalizedInput.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase)
                         || normalizedCandidate.Contains(normalizedInput, StringComparison.OrdinalIgnoreCase))
                {
                    confidence = 0.85;
                }
                else
                {
                    continue;
                }

                if (bestMatch is null || confidence > bestMatch.Confidence)
                {
                    bestMatch = new IndicatorMatchResult(indicator, confidence, candidate.Text);
                }
            }
        }

        return bestMatch;
    }

    private static IEnumerable<(string Text, bool IsPrimary)> GetMatchCandidates(LabIndicatorMaster indicator)
    {
        if (!string.IsNullOrWhiteSpace(indicator.Symbol))
        {
            yield return (indicator.Symbol, false);
        }

        if (!string.IsNullOrWhiteSpace(indicator.FullName))
        {
            yield return (indicator.FullName, true);
        }

        foreach (var alias in indicator.LabIndicatorAliases.Where(a => !a.IsDeleted))
        {
            yield return (alias.AliasText, alias.IsPrimary);
        }
    }

    private static string Normalize(string value)
    {
        return value.Trim().TrimEnd('.', ':');
    }
}
