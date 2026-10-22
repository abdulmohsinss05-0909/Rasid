using Rasid.Domain.Common;

namespace Rasid.Domain.Engagement;

/// <summary>
/// Boundaries between engagement levels, in percent. Defaults follow common social-media
/// benchmarks (an assumption, not an X standard) and are configurable per environment.
/// Low: rate &lt; <see cref="MediumFromPercent"/>; Medium: up to and including
/// <see cref="HighAbovePercent"/>; High: above it.
/// </summary>
public sealed record EngagementThresholds
{
    public EngagementThresholds(decimal mediumFromPercent = 1m, decimal highAbovePercent = 3m)
    {
        if (mediumFromPercent <= 0 || highAbovePercent <= mediumFromPercent)
        {
            throw new DomainException("Engagement thresholds must satisfy 0 < medium-from < high-above.");
        }

        MediumFromPercent = mediumFromPercent;
        HighAbovePercent = highAbovePercent;
    }

    public static EngagementThresholds Default { get; } = new();

    public decimal MediumFromPercent { get; }

    public decimal HighAbovePercent { get; }
}
