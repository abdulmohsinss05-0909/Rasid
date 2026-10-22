using Rasid.Domain.Common;

namespace Rasid.Domain.Engagement;

/// <summary>Append-only, point-in-time record of a post's metrics. Enables growth-over-time analysis.</summary>
public sealed class MetricSnapshot : Entity<long>
{
    private MetricSnapshot()
    {
    }

    public string PostId { get; private set; } = string.Empty;

    public DateTimeOffset CapturedUtc { get; private set; }

    public EngagementMetrics Metrics { get; private set; } = EngagementMetrics.Empty;

    public static MetricSnapshot Capture(string postId, EngagementMetrics metrics, DateTimeOffset capturedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postId);
        ArgumentNullException.ThrowIfNull(metrics);

        return new MetricSnapshot { PostId = postId, Metrics = metrics, CapturedUtc = capturedUtc };
    }
}
