using System.Text.RegularExpressions;

namespace Rasid.Domain.Insights;

/// <summary>
/// Pure, unit-tested analytics helpers used by the insight read models. They work only on data
/// Rasid already holds, so no insight costs extra X API calls.
/// </summary>
public static partial class InsightMath
{
    /// <summary>Period-over-period change in percent; null when there is no previous value to compare with.</summary>
    public static decimal? PercentChange(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round((current - previous) * 100m / previous, 1, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Flags points that are unusually high: above mean + <paramref name="sigmas"/> standard deviations
    /// and at least <paramref name="minimum"/>. Short series are never flagged.
    /// </summary>
    public static bool[] DetectSpikes(IReadOnlyList<long> values, double sigmas = 2.0, long minimum = 3, int minimumPoints = 6)
    {
        ArgumentNullException.ThrowIfNull(values);

        var flags = new bool[values.Count];
        if (values.Count < minimumPoints)
        {
            return flags;
        }

        var mean = values.Average();
        var deviation = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
        if (deviation == 0)
        {
            return flags;
        }

        var threshold = mean + (sigmas * deviation);
        for (var i = 0; i < values.Count; i++)
        {
            flags[i] = values[i] >= minimum && values[i] > threshold;
        }

        return flags;
    }

    /// <summary>Distinct hashtags of a text (without '#'), in order of appearance; case-insensitive.</summary>
    public static IReadOnlyList<string> ExtractHashtags(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tags = new List<string>();
        foreach (Match match in HashtagPattern().Matches(text))
        {
            var tag = match.Groups[1].Value;
            if (seen.Add(tag))
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}_])#([\p{L}\p{N}_]{2,100})")]
    private static partial Regex HashtagPattern();
}
