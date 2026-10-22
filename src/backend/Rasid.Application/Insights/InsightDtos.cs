using Rasid.Domain.Topics;

namespace Rasid.Application.Insights;

/// <summary>Analytical breakdowns of a topic for the current filter. Computed from stored data only.</summary>
public sealed record TopicInsightsDto(
    long PostsAnalysed,
    bool IsTruncated,
    IReadOnlyList<HighlightDto> Highlights,
    IReadOnlyList<HashtagInsightDto> TopHashtags,
    IReadOnlyList<AccountInsightDto> TopAccounts,
    ActivityHeatmapDto Heatmap,
    IReadOnlyList<RuleInsightDto> Rules,
    IReadOnlyList<LanguageInsightDto> Languages);

/// <summary>A plain-language finding, e.g. "#UAEAid appears in 62% of posts".</summary>
public sealed record HighlightDto(string Icon, string Title, string Detail);

public sealed record HashtagInsightDto(string Tag, long Posts, long Interactions, decimal SharePercent);

public sealed record AccountInsightDto(
    string AuthorId,
    string Username,
    string DisplayName,
    string? ProfileImageUrl,
    bool IsVerified,
    long Posts,
    long Interactions,
    decimal? EngagementRate,
    decimal InteractionSharePercent);

/// <summary>Posts per weekday (0 = Sunday … 6 = Saturday) and hour (0–23), in the viewer's local time.</summary>
public sealed record ActivityHeatmapDto(IReadOnlyList<IReadOnlyList<long>> Cells, long Max, int PeakDay, int PeakHour);

public sealed record RuleInsightDto(RuleType Type, string DisplayValue, long Posts, long Interactions, decimal SharePercent);

public sealed record LanguageInsightDto(string Code, string Label, long Posts, decimal SharePercent);
