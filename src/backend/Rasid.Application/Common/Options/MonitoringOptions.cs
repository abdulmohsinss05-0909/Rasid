using System.ComponentModel.DataAnnotations;
using Rasid.Domain.Engagement;
using Rasid.Domain.Quota;
using Rasid.Domain.Topics;

namespace Rasid.Application.Common.Options;

/// <summary>Business configuration for monitoring. Bound from the "Monitoring" section and validated at startup.</summary>
public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>When false (Production), the Sample connector can never be used.</summary>
    public bool AllowSampleData { get; set; }

    [Range(1, 1440)]
    public int HighPriorityRefreshMinutes { get; set; } = 10;

    [Range(1, 1440)]
    public int NormalPriorityRefreshMinutes { get; set; } = 15;

    [Range(1, 1440)]
    public int LowPriorityRefreshMinutes { get; set; } = 60;

    /// <summary>Post text and author details are purged after this many days.</summary>
    [Range(1, 365)]
    public int ContentRetentionDays { get; set; } = 30;

    /// <summary>Metric snapshots (history) are purged after this many days.</summary>
    [Range(1, 365)]
    public int SnapshotRetentionDays { get; set; } = 30;

    /// <summary>Hourly aggregates (no personal data) are kept for this many days.</summary>
    [Range(1, 1095)]
    public int AggregateRetentionDays { get; set; } = 400;

    /// <summary>Maximum query length allowed by the purchased X plan.</summary>
    [Range(64, 4096)]
    public int MaxQueryLength { get; set; } = 512;

    [Range(10, 100)]
    public int PageSize { get; set; } = 100;

    [Range(0, 3600)]
    public int DashboardCacheSeconds { get; set; } = 60;

    /// <summary>Minimum time between two manual "refresh now" requests for the same topic.</summary>
    [Range(0, 3600)]
    public int ManualRefreshCooldownSeconds { get; set; } = 60;

    [Required]
    public EngagementOptions Engagement { get; set; } = new();

    [Required]
    public QuotaOptions Quota { get; set; } = new();

    [Required]
    public MetricRefreshOptions MetricRefresh { get; set; } = new();

    public TimeSpan RefreshInterval(TopicPriority priority) => TimeSpan.FromMinutes(priority switch
    {
        TopicPriority.High => HighPriorityRefreshMinutes,
        TopicPriority.Low => LowPriorityRefreshMinutes,
        _ => NormalPriorityRefreshMinutes,
    });

    public TimeSpan ContentRetention => TimeSpan.FromDays(ContentRetentionDays);
}

public sealed class EngagementOptions
{
    [Range(0.01, 100)]
    public decimal MediumFromPercent { get; set; } = 1m;

    [Range(0.01, 100)]
    public decimal HighAbovePercent { get; set; } = 3m;

    public EngagementThresholds ToThresholds() => new(MediumFromPercent, HighAbovePercent);
}

public sealed class QuotaOptions
{
    /// <summary>Posts that may be read per month under the purchased X plan; 0 = no monthly cap.</summary>
    [Range(0, long.MaxValue)]
    public long MonthlyPostReadBudget { get; set; }

    [Range(1, 99)]
    public decimal ThrottleAtPercent { get; set; } = 80m;

    [Range(2, 100)]
    public decimal CriticalAtPercent { get; set; } = 95m;

    [Range(1, 20)]
    public int MaxPagesPerRun { get; set; } = 3;

    public QuotaSettings ToSettings() => new(MonthlyPostReadBudget, ThrottleAtPercent, CriticalAtPercent, MaxPagesPerRun);
}

/// <summary>Tiered metric refresh: young posts change fast, so they are refreshed more often.</summary>
public sealed class MetricRefreshOptions
{
    [Range(1, 1440)]
    public int FreshPostMinutes { get; set; } = 15;

    [Range(1, 1440)]
    public int RecentPostMinutes { get; set; } = 120;

    [Range(1, 10080)]
    public int OlderPostMinutes { get; set; } = 1440;

    /// <summary>Upper bound of posts refreshed per run, to keep each run's API cost predictable.</summary>
    [Range(100, 10_000)]
    public int MaxPostsPerRun { get; set; } = 500;
}
