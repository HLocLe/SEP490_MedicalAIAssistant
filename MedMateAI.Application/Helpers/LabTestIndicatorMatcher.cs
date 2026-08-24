using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MedMateAI.Domain.Entities;

namespace MedMateAI.Application.Helpers;

public sealed record IndicatorMatchResult(
    LabIndicatorMaster Indicator,
    double Confidence,
    string MatchedText);

public static class LabTestIndicatorMatcher
{
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static IndicatorMatchResult? Match(
        string testName,
        IReadOnlyList<LabIndicatorMaster> indicators)
    {
        if (string.IsNullOrWhiteSpace(testName) || indicators.Count == 0)
        {
            return null;
        }

        var normalizedInput = Normalize(testName);
        if (string.IsNullOrEmpty(normalizedInput))
        {
            return null;
        }

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
                if (string.Equals(normalizedInput, normalizedCandidate, StringComparison.Ordinal))
                {
                    confidence = candidate.IsPrimary ? 1.0 : 0.95;
                }
                else if (normalizedInput.Contains(normalizedCandidate, StringComparison.Ordinal)
                         || normalizedCandidate.Contains(normalizedInput, StringComparison.Ordinal))
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
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var collapsed = MultiWhitespaceRegex.Replace(value.Trim(), " ");
        collapsed = collapsed.TrimEnd('.', ':').Trim();

        var withoutDiacritics = RemoveDiacritics(collapsed);
        return withoutDiacritics.ToLowerInvariant();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
