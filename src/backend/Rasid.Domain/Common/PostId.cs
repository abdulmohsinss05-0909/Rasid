namespace Rasid.Domain.Common;

/// <summary>Helpers for X post identifiers (Snowflake IDs: numeric strings that grow over time).</summary>
public static class PostId
{
    /// <summary>Returns true when <paramref name="candidate"/> is a newer post than <paramref name="current"/>.</summary>
    public static bool IsNewer(string candidate, string? current)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);

        if (string.IsNullOrEmpty(current))
        {
            return true;
        }

        // Numeric strings of different length: the longer one is larger.
        return candidate.Length != current.Length
            ? candidate.Length > current.Length
            : string.CompareOrdinal(candidate, current) > 0;
    }

    /// <summary>X epoch used by Snowflake IDs (milliseconds since Unix epoch).</summary>
    private const long SnowflakeEpochMilliseconds = 1_288_834_974_657;

    /// <summary>
    /// Creation time encoded in an X Snowflake ID (the top 41 bits are milliseconds since the X epoch).
    /// Returns null for IDs that are not Snowflakes (e.g. sample data).
    /// </summary>
    public static DateTimeOffset? SnowflakeTime(string? id)
    {
        if (id is not { Length: >= 15 } || !ulong.TryParse(id, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds((long)(value >> 22) + SnowflakeEpochMilliseconds);
    }

    /// <summary>Returns the newest ID from a sequence, or null when empty.</summary>
    public static string? Newest(IEnumerable<string> ids)
    {
        string? newest = null;
        foreach (var id in ids)
        {
            if (IsNewer(id, newest))
            {
                newest = id;
            }
        }

        return newest;
    }
}
