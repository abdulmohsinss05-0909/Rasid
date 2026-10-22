using Rasid.Application.Connectors;
using Rasid.Domain.Topics;

namespace Rasid.Application.Dashboard;

/// <summary>
/// The earliest moment for which a topic's data is complete: its first search reached back one
/// search window before the topic was created. Comparisons with periods before this would compare
/// against missing data, so they are reported as "new" instead of as an inflated percentage.
/// </summary>
internal static class DataCoverage
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(7);

    public static DateTimeOffset Start(Topic topic, IPostSource? source) =>
        topic.CreatedUtc - (source?.SearchWindow ?? DefaultWindow);

    public static bool Covers(Topic topic, IPostSource? source, DateTimeOffset fromUtc) =>
        fromUtc >= Start(topic, source);
}
