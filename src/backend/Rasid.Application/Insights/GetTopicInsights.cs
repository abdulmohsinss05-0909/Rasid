using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;
using Rasid.Application.Dashboard;
using Rasid.Application.Topics;
using Rasid.Domain.Engagement;
using Rasid.Domain.Insights;
using Rasid.Domain.Topics;

namespace Rasid.Application.Insights;

public sealed record GetTopicInsightsQuery(
    Guid TopicId,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Keyword = null,
    EngagementType EngagementType = EngagementType.All,
    EngagementLevel? Level = null,
    int UtcOffsetMinutes = 0) : IQuery<TopicInsightsDto>, IPostFilterRequest;

internal sealed class GetTopicInsightsValidator : AbstractValidator<GetTopicInsightsQuery>
{
    public GetTopicInsightsValidator()
    {
        this.ApplyFilterRules();
        RuleFor(q => q.UtcOffsetMinutes).InclusiveBetween(-DashboardRange.MaxUtcOffsetMinutes, DashboardRange.MaxUtcOffsetMinutes);
    }
}

/// <summary>
/// Hashtags, accounts, activity pattern, rule performance and language mix for the filtered posts.
/// Works on a bounded, indexed slice (one topic, one period) and is cached like the dashboard.
/// </summary>
internal sealed class GetTopicInsightsHandler(
    IRasidDbContext db,
    HybridCache cache,
    IOptions<MonitoringOptions> options,
    TimeProvider clock) : IRequestHandler<GetTopicInsightsQuery, TopicInsightsDto>
{
    /// <summary>Upper bound of posts analysed per request, to keep latency predictable.</summary>
    private const int MaxPosts = 20_000;
    private const int TopCount = 8;

    private static readonly string[] DayNames = CultureInfo.InvariantCulture.DateTimeFormat.DayNames;

    public async Task<Result<TopicInsightsDto>> HandleAsync(GetTopicInsightsQuery request, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.AsNoTracking().Include(t => t.Rules)
            .FirstOrDefaultAsync(t => t.Id == request.TopicId, cancellationToken);
        if (topic is null)
        {
            return TopicErrors.NotFound(request.TopicId);
        }

        var (from, to) = DashboardRange.Resolve(request.FromUtc, request.ToUtc, clock.GetUtcNow());
        if ((to - from).TotalDays > DashboardRange.MaxRangeDays)
        {
            return Error.Validation("fromUtc", $"The period cannot exceed {DashboardRange.MaxRangeDays} days.");
        }

        var cacheKey = string.Join(
            ':',
            "insights",
            topic.Id.ToString("N"),
            (topic.LastRefreshAttemptUtc ?? topic.CreatedUtc).UtcTicks.ToString(CultureInfo.InvariantCulture),
            request.FromUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "default",
            request.ToUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "now",
            request.Keyword?.Trim().ToUpperInvariant() ?? string.Empty,
            request.Level?.ToString() ?? "any",
            request.UtcOffsetMinutes.ToString(CultureInfo.InvariantCulture));

        var seconds = Math.Max(1, options.Value.DashboardCacheSeconds);
        return await cache.GetOrCreateAsync(
            cacheKey,
            (Topic: topic, From: from, To: to, Request: request),
            async (state, ct) => await BuildAsync(state.Topic, state.From, state.To, state.Request, ct),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(seconds), LocalCacheExpiration = TimeSpan.FromSeconds(seconds) },
            tags: [$"topic:{topic.Id:N}"],
            cancellationToken: cancellationToken);
    }

    private async Task<TopicInsightsDto> BuildAsync(
        Topic topic, DateTimeOffset from, DateTimeOffset to, GetTopicInsightsQuery request, CancellationToken cancellationToken)
    {
        var thresholds = options.Value.Engagement.ToThresholds();

        var rows = await db.Posts.AsNoTracking()
            .ForTopic(db.TopicPosts, topic.Id)
            .CreatedBetween(from, to)
            .ContainingKeyword(request.Keyword)
            .WithLevel(request.Level, thresholds)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(MaxPosts + 1)
            .Select(p => new PostRow(
                p.CreatedAtUtc,
                p.Text,
                p.Language,
                p.AuthorId,
                p.Author!.Username,
                p.Author.DisplayName,
                p.Author.ProfileImageUrl,
                p.Author.IsVerified,
                p.Metrics.Likes + p.Metrics.Reposts + p.Metrics.Replies + p.Metrics.Quotes,
                p.Metrics.Views))
            .ToListAsync(cancellationToken);

        var truncated = rows.Count > MaxPosts;
        if (truncated)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var tagged = rows.Select(r => (Row: r, Tags: InsightMath.ExtractHashtags(r.Text))).ToList();
        var hashtags = TopHashtags(tagged, rows.Count);
        var accounts = TopAccounts(rows);
        var heatmap = Heatmap(rows, TimeSpan.FromMinutes(request.UtcOffsetMinutes));
        var rules = RulePerformance(topic, tagged, rows.Count);
        var languages = Languages(rows);

        return new TopicInsightsDto(
            rows.Count,
            truncated,
            Highlights(rows, hashtags, accounts, heatmap, rules, languages),
            hashtags,
            accounts,
            heatmap,
            rules,
            languages);
    }

    private static List<HashtagInsightDto> TopHashtags(List<(PostRow Row, IReadOnlyList<string> Tags)> tagged, int total) =>
        [.. tagged
            .SelectMany(t => t.Tags.Select(tag => (Tag: tag, t.Row)))
            .GroupBy(x => x.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(g => new HashtagInsightDto(
                g.First().Tag,
                g.LongCount(),
                g.Sum(x => x.Row.Interactions),
                Share(g.LongCount(), total)))
            .OrderByDescending(h => h.Posts)
            .ThenByDescending(h => h.Interactions)
            .Take(TopCount)];

    private static List<AccountInsightDto> TopAccounts(List<PostRow> rows)
    {
        var totalInteractions = rows.Sum(r => r.Interactions);
        return [.. rows
            .GroupBy(r => r.AuthorId, StringComparer.Ordinal)
            .Select(g =>
            {
                var first = g.First();
                var interactions = g.Sum(r => r.Interactions);
                var viewed = g.Where(r => r.Views is > 0).ToList();
                return new AccountInsightDto(
                    g.Key,
                    first.Username,
                    first.DisplayName,
                    first.ProfileImageUrl,
                    first.IsVerified,
                    g.LongCount(),
                    interactions,
                    EngagementCalculator.WeightedRate(viewed.Sum(r => r.Interactions), viewed.Sum(r => r.Views!.Value)),
                    Share(interactions, totalInteractions));
            })
            .OrderByDescending(a => a.Interactions)
            .ThenByDescending(a => a.Posts)
            .Take(TopCount)];
    }

    private static ActivityHeatmapDto Heatmap(List<PostRow> rows, TimeSpan offset)
    {
        var cells = new long[7][];
        for (var day = 0; day < 7; day++)
        {
            cells[day] = new long[24];
        }

        foreach (var row in rows)
        {
            var local = row.CreatedAtUtc.ToUniversalTime() + offset;
            cells[(int)local.DayOfWeek][local.Hour]++;
        }

        var (peakDay, peakHour, max) = (0, 0, 0L);
        for (var day = 0; day < 7; day++)
        {
            for (var hour = 0; hour < 24; hour++)
            {
                if (cells[day][hour] > max)
                {
                    (peakDay, peakHour, max) = (day, hour, cells[day][hour]);
                }
            }
        }

        return new ActivityHeatmapDto([.. cells.Select(day => (IReadOnlyList<long>)day)], max, peakDay, peakHour);
    }

    private static List<RuleInsightDto> RulePerformance(Topic topic, List<(PostRow Row, IReadOnlyList<string> Tags)> tagged, int total) =>
        [.. topic.Rules
            .Where(r => !r.IsExclusion)
            .Select(rule =>
            {
                var matches = tagged.Where(t => Matches(rule, t.Row, t.Tags)).ToList();
                return new RuleInsightDto(
                    rule.Type,
                    rule.DisplayValue,
                    matches.Count,
                    matches.Sum(m => m.Row.Interactions),
                    Share(matches.Count, total));
            })
            .OrderByDescending(r => r.Posts)];

    private static bool Matches(TopicRule rule, PostRow row, IReadOnlyList<string> tags) => rule.Type switch
    {
        RuleType.Hashtag => tags.Contains(rule.Value, StringComparer.OrdinalIgnoreCase),
        RuleType.Account => string.Equals(row.Username, rule.Value, StringComparison.OrdinalIgnoreCase),
        _ => row.Text.Contains(rule.Value, StringComparison.OrdinalIgnoreCase),
    };

    private static List<LanguageInsightDto> Languages(List<PostRow> rows) =>
        [.. rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Language) ? "und" : r.Language.ToLowerInvariant(), StringComparer.Ordinal)
            .Select(g => new LanguageInsightDto(g.Key, LanguageLabel(g.Key), g.LongCount(), Share(g.LongCount(), rows.Count)))
            .OrderByDescending(l => l.Posts)];

    private static List<HighlightDto> Highlights(
        List<PostRow> rows,
        List<HashtagInsightDto> hashtags,
        List<AccountInsightDto> accounts,
        ActivityHeatmapDto heatmap,
        List<RuleInsightDto> rules,
        List<LanguageInsightDto> languages)
    {
        var highlights = new List<HighlightDto>();
        if (rows.Count == 0)
        {
            return highlights;
        }

        if (hashtags.FirstOrDefault() is { } tag)
        {
            highlights.Add(new HighlightDto("tag", $"#{tag.Tag} leads the conversation", $"Appears in {tag.SharePercent}% of posts ({tag.Posts:N0})."));
        }

        if (accounts.FirstOrDefault() is { Interactions: > 0 } account)
        {
            highlights.Add(new HighlightDto("person", $"@{account.Username} drives the most engagement", $"{account.InteractionSharePercent}% of all interactions from {account.Posts:N0} post(s)."));
        }

        if (heatmap.Max > 0)
        {
            highlights.Add(new HighlightDto("schedule", $"Peak activity: {DayNames[heatmap.PeakDay]}s around {heatmap.PeakHour:00}:00", "Local time, based on when posts were published."));
        }

        if (rules.FirstOrDefault() is { Posts: > 0 } rule)
        {
            highlights.Add(new HighlightDto("rule", $"\"{rule.DisplayValue}\" is the strongest rule", $"Matches {rule.SharePercent}% of the topic's posts."));
        }

        if (languages.Count > 1)
        {
            highlights.Add(new HighlightDto("translate", "Multilingual conversation", string.Join(" · ", languages.Take(3).Select(l => $"{l.Label} {l.SharePercent}%"))));
        }

        return highlights;
    }

    private static string LanguageLabel(string code) => code switch
    {
        "en" => "English",
        "ar" => "Arabic",
        "fr" => "French",
        "ur" => "Urdu",
        "hi" => "Hindi",
        "und" or "zxx" or "qme" or "qam" => "Undetermined",
        _ => code.ToUpperInvariant(),
    };

    private static decimal Share(long part, long total) =>
        total == 0 ? 0 : Math.Round(part * 100m / total, 1, MidpointRounding.AwayFromZero);

    private sealed record PostRow(
        DateTimeOffset CreatedAtUtc,
        string Text,
        string? Language,
        string AuthorId,
        string Username,
        string DisplayName,
        string? ProfileImageUrl,
        bool IsVerified,
        long Interactions,
        long? Views);
}
