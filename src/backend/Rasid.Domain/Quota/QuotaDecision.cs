namespace Rasid.Domain.Quota;

public enum QuotaMode
{
    /// <summary>Budget healthy: normal cadence.</summary>
    Normal = 0,

    /// <summary>Budget above the throttle threshold: lower-priority topics refresh less often.</summary>
    Throttled = 1,

    /// <summary>Budget nearly exhausted: only high-priority topics, minimal pages.</summary>
    Critical = 2,

    /// <summary>No call allowed now.</summary>
    Denied = 3,
}

/// <summary>Answer of the quota policy to "may this topic call X now, and how much?".</summary>
public sealed record QuotaDecision(
    QuotaMode Mode,
    int MaxPages,
    int IntervalMultiplier,
    DateTimeOffset? RetryAfterUtc,
    string Reason)
{
    public bool IsAllowed => Mode != QuotaMode.Denied;

    public decimal? BudgetUsedPercent { get; init; }
}
