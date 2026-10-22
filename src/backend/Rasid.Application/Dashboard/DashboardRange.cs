namespace Rasid.Application.Dashboard;

/// <summary>Resolves the requested period (defaults to the last 7 days) and the chart bucket size.</summary>
internal static class DashboardRange
{
    public const int MaxRangeDays = 92;
    public const int DefaultRangeDays = 7;
    public const int MaxUtcOffsetMinutes = 14 * 60;
    private static readonly TimeSpan HourlyUpTo = TimeSpan.FromDays(3);

    /// <summary>
    /// The start is aligned down to the hour so the pre-aggregated path (hourly rows) and the
    /// post-level path always count exactly the same posts.
    /// </summary>
    public static (DateTimeOffset From, DateTimeOffset To) Resolve(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, DateTimeOffset nowUtc)
    {
        var to = toUtc?.ToUniversalTime() ?? nowUtc;
        var from = fromUtc?.ToUniversalTime() ?? to.AddDays(-DefaultRangeDays);
        return (Domain.Engagement.TopicMetricsHourly.TruncateToHour(from), to);
    }

    public static SeriesGranularity GranularityFor(DateTimeOffset from, DateTimeOffset to) =>
        to - from <= HourlyUpTo ? SeriesGranularity.Hour : SeriesGranularity.Day;

    /// <summary>
    /// Start of the bucket containing <paramref name="instant"/>. Day buckets follow the viewer's
    /// local day (via <paramref name="utcOffset"/>); hour buckets are UTC hours.
    /// </summary>
    public static DateTimeOffset BucketStart(DateTimeOffset instant, SeriesGranularity granularity, TimeSpan utcOffset)
    {
        var utc = instant.ToUniversalTime();
        if (granularity == SeriesGranularity.Hour)
        {
            return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
        }

        var local = utc + utcOffset;
        return new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, TimeSpan.Zero) - utcOffset;
    }

    public static TimeSpan Step(SeriesGranularity granularity) =>
        granularity == SeriesGranularity.Hour ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
}
