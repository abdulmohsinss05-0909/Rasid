using Rasid.Application.Topics;
using Rasid.Domain.Common;
using Rasid.Domain.Engagement;
using Rasid.Domain.Topics;

namespace Rasid.Application.Dashboard;

public enum SeriesGranularity
{
    Hour = 1,
    Day = 2,
}

public enum PostSort
{
    Recent = 0,
    MostEngaged = 1,
    HighestRate = 2,
}

public sealed record DashboardDto(
    TopicDto Topic,
    DataFreshnessDto Freshness,
    AppliedFilterDto Filter,
    KpiDto Kpis,
    TrendsDto Trends,
    IReadOnlyList<SeriesPointDto> Series,
    EngagementBreakdownDto Breakdown,
    LevelDistributionDto Levels,
    IReadOnlyList<PostDto> TopPosts,
    EngagementMethodDto Method);

/// <summary>Where the data came from, how fresh it is, and what to tell the user about it.</summary>
public sealed record DataFreshnessDto(
    DataSource? Source,
    string SourceLabel,
    bool IsSampleData,
    RefreshStatus Status,
    bool IsStale,
    DateTimeOffset? LastUpdatedUtc,
    DateTimeOffset? LastAttemptUtc,
    DateTimeOffset? RetryAfterUtc,
    string Message,
    DateTimeOffset GeneratedUtc);

public sealed record AppliedFilterDto(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? Keyword,
    EngagementType EngagementType,
    EngagementLevel? Level,
    SeriesGranularity Granularity,
    bool UsesPreAggregatedData);

public sealed record KpiDto(
    long Posts,
    long Likes,
    long Reposts,
    long Replies,
    long Quotes,
    long Interactions,
    long? Views,
    decimal? EngagementRate,
    EngagementLevel EngagementLevel,
    long PostsWithViews,
    long SelectedTypeTotal);

public sealed record SeriesPointDto(
    DateTimeOffset BucketStartUtc,
    long Posts,
    long Value,
    long Interactions,
    long Views,
    decimal? EngagementRate,
    long Likes,
    long Reposts,
    long Replies,
    long Quotes,
    bool IsSpike);

/// <summary>
/// Change versus the previous period of the same length, in percent (rate: in percentage points).
/// Null when there is nothing to compare with, or when <see cref="IsComparable"/> is false because the
/// previous period lies before the topic's data coverage.
/// </summary>
public sealed record TrendsDto(
    DateTimeOffset PreviousFromUtc,
    DateTimeOffset PreviousToUtc,
    long PreviousPosts,
    bool IsComparable,
    decimal? Posts,
    decimal? Interactions,
    decimal? SelectedType,
    decimal? Likes,
    decimal? Reposts,
    decimal? Replies,
    decimal? Quotes,
    decimal? Views,
    decimal? EngagementRatePoints);

public sealed record EngagementBreakdownDto(long Likes, long Reposts, long Replies, long Quotes);

public sealed record LevelDistributionDto(long High, long Medium, long Low, long Unknown);

public sealed record MediaDto(string Kind, string PreviewUrl, string? AltText);

public sealed record AuthorDto(string Id, string Username, string DisplayName, string? ProfileImageUrl, bool IsVerified);

public sealed record MetricsDto(long Likes, long Reposts, long Replies, long Quotes, long? Views, long Interactions);

public sealed record PostDto(
    string Id,
    string Text,
    string? Language,
    DateTimeOffset CreatedAtUtc,
    string? Url,
    DataSource Source,
    AuthorDto Author,
    MetricsDto Metrics,
    decimal? EngagementRate,
    EngagementLevel EngagementLevel,
    DateTimeOffset MetricsCapturedUtc,
    MediaDto? Media);

/// <summary>The engagement method, shipped with the data so the UI can always explain the numbers.</summary>
public sealed record EngagementMethodDto(
    string InteractionsFormula,
    string RateFormula,
    string TopicRateFormula,
    decimal MediumFromPercent,
    decimal HighAbovePercent,
    string Notes);
