using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;
using Rasid.Application.Topics;
using Rasid.Domain.Engagement;
using Rasid.Domain.Insights;
using Rasid.Domain.Topics;

namespace Rasid.Application.Dashboard;

public sealed record GetTopicDashboardQuery(
    Guid TopicId,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Keyword = null,
    EngagementType EngagementType = EngagementType.All,
    EngagementLevel? Level = null,
    int UtcOffsetMinutes = 0) : IQuery<DashboardDto>, IPostFilterRequest;

internal sealed class GetTopicDashboardValidator : AbstractValidator<GetTopicDashboardQuery>
{
    public GetTopicDashboardValidator()
    {
        this.ApplyFilterRules();
        RuleFor(q => q.UtcOffsetMinutes).InclusiveBetween(-DashboardRange.MaxUtcOffsetMinutes, DashboardRange.MaxUtcOffsetMinutes);
    }
}

/// <summary>
/// Read side of CQRS-lite. Without post-level filters the dashboard is served from the pre-aggregated
/// hourly table; keyword or level filters switch to an indexed, topic- and period-bounded post query.
/// Results are cached with a key that includes the topic's last refresh time, so new data is never hidden.
/// </summary>
internal sealed class GetTopicDashboardHandler(
    IRasidDbContext db,
    DataFreshnessBuilder freshnessBuilder,
    Connectors.IPostSourceResolver sourceResolver,
    HybridCache cache,
    IOptions<MonitoringOptions> options,
    TimeProvider clock) : IRequestHandler<GetTopicDashboardQuery, DashboardDto>
{
    private const int TopPostCount = 5;

    public async Task<Result<DashboardDto>> HandleAsync(GetTopicDashboardQuery request, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.AsNoTracking().Include(t => t.Rules)
            .FirstOrDefaultAsync(t => t.Id == request.TopicId, cancellationToken);
        if (topic is null)
        {
            return TopicErrors.NotFound(request.TopicId);
        }

        var now = clock.GetUtcNow();
        var (from, to) = DashboardRange.Resolve(request.FromUtc, request.ToUtc, now);
        if ((to - from).TotalDays > DashboardRange.MaxRangeDays)
        {
            return Error.Validation("fromUtc", $"The period cannot exceed {DashboardRange.MaxRangeDays} days.");
        }

        var cacheKey = string.Join(
            ':',
            "dashboard",
            topic.Id.ToString("N"),
            (topic.LastRefreshAttemptUtc ?? topic.CreatedUtc).UtcTicks.ToString(CultureInfo.InvariantCulture),
            request.FromUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "default",
            request.ToUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "now",
            request.Keyword?.Trim().ToUpperInvariant() ?? string.Empty,
            request.EngagementType,
            request.Level?.ToString() ?? "any",
            request.UtcOffsetMinutes.ToString(CultureInfo.InvariantCulture));

        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromSeconds(options.Value.DashboardCacheSeconds),
            LocalCacheExpiration = TimeSpan.FromSeconds(options.Value.DashboardCacheSeconds),
        };

        return await cache.GetOrCreateAsync(
            cacheKey,
            (Topic: topic, From: from, To: to, Request: request),
            async (state, ct) => await BuildAsync(state.Topic, state.From, state.To, state.Request, ct),
            entryOptions,
            tags: [$"topic:{topic.Id:N}"],
            cancellationToken: cancellationToken);
    }

    private async Task<DashboardDto> BuildAsync(
        Topic topic, DateTimeOffset from, DateTimeOffset to, GetTopicDashboardQuery request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var thresholds = settings.Engagement.ToThresholds();
        var granularity = DashboardRange.GranularityFor(from, to);
        var offset = TimeSpan.FromMinutes(request.UtcOffsetMinutes);
        var usesAggregates = string.IsNullOrWhiteSpace(request.Keyword) && request.Level is null;

        var buckets = usesAggregates
            ? await BucketsFromAggregatesAsync(topic.Id, from, to, granularity, offset, cancellationToken)
            : await BucketsFromPostsAsync(topic.Id, from, to, request, thresholds, granularity, offset, cancellationToken);

        var total = buckets.Values.Aggregate(EngagementSummary.Empty, (sum, bucket) => sum.Add(bucket));
        var levels = total.Levels ?? LevelCounts.None;

        // Previous period of the same length, through the same path, for period-over-period trends.
        var previousFrom = from - (to - from);
        var previousTo = from.AddTicks(-1);
        var previousBuckets = usesAggregates
            ? await BucketsFromAggregatesAsync(topic.Id, previousFrom, previousTo, granularity, offset, cancellationToken)
            : await BucketsFromPostsAsync(topic.Id, previousFrom, previousTo, request, thresholds, granularity, offset, cancellationToken);
        var previous = previousBuckets.Values.Aggregate(EngagementSummary.Empty, (sum, bucket) => sum.Add(bucket));

        var topPosts = await db.Posts.AsNoTracking()
            .ForTopic(db.TopicPosts, topic.Id)
            .CreatedBetween(from, to)
            .ContainingKeyword(request.Keyword)
            .WithLevel(request.Level, thresholds)
            .SortBy(PostSort.MostEngaged, request.EngagementType)
            .Take(TopPostCount)
            .ToDtos()
            .ToListAsync(cancellationToken);

        var postCount = await db.TopicPosts.LongCountAsync(tp => tp.TopicId == topic.Id, cancellationToken);

        return new DashboardDto(
            topic.ToDto(postCount, settings, clock.GetUtcNow()),
            await freshnessBuilder.BuildAsync(topic, cancellationToken),
            new AppliedFilterDto(from, to, request.Keyword, request.EngagementType, request.Level, granularity, usesAggregates),
            new KpiDto(
                total.Posts,
                total.Likes,
                total.Reposts,
                total.Replies,
                total.Quotes,
                total.Interactions,
                total.ViewedPosts > 0 ? total.Views : null,
                total.Rate,
                EngagementCalculator.Level(total.Rate, thresholds),
                total.ViewedPosts,
                ValueOf(total, request.EngagementType)),
            BuildTrends(total, previous, previousFrom, from, request.EngagementType, DataCoverage.Covers(topic, sourceResolver.Resolve(), previousFrom)),
            BuildSeries(buckets, from, to, granularity, offset, request.EngagementType),
            new EngagementBreakdownDto(total.Likes, total.Reposts, total.Replies, total.Quotes),
            new LevelDistributionDto(levels.High, levels.Medium, levels.Low, levels.Unknown),
            [.. topPosts.Select(p => p.WithEngagement(thresholds))],
            new EngagementMethodDto(
                "Interactions = Likes + Reposts + Replies + Quotes",
                "Post engagement rate (%) = Interactions ÷ Views × 100",
                "Topic engagement rate (%) = Σ Interactions ÷ Σ Views, over posts that have view counts",
                thresholds.MediumFromPercent,
                thresholds.HighAbovePercent,
                "Rates are N/A when X does not provide view counts; they are never estimated. Level thresholds are configurable benchmarks, not an X standard."));
    }

    private async Task<Dictionary<DateTimeOffset, EngagementSummary>> BucketsFromAggregatesAsync(
        Guid topicId, DateTimeOffset from, DateTimeOffset to, SeriesGranularity granularity, TimeSpan offset, CancellationToken cancellationToken)
    {
        var fromHour = TopicMetricsHourly.TruncateToHour(from);
        var rows = await db.TopicMetricsHourly.AsNoTracking()
            .Where(h => h.TopicId == topicId && h.HourUtc >= fromHour && h.HourUtc <= to)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(h => DashboardRange.BucketStart(h.HourUtc, granularity, offset))
            .ToDictionary(g => g.Key, g => g.Select(h => h.ToSummary()).Aggregate(EngagementSummary.Empty, (a, b) => a.Add(b)));
    }

    private async Task<Dictionary<DateTimeOffset, EngagementSummary>> BucketsFromPostsAsync(
        Guid topicId,
        DateTimeOffset from,
        DateTimeOffset to,
        GetTopicDashboardQuery request,
        EngagementThresholds thresholds,
        SeriesGranularity granularity,
        TimeSpan offset,
        CancellationToken cancellationToken)
    {
        var rows = await db.Posts.AsNoTracking()
            .ForTopic(db.TopicPosts, topicId)
            .CreatedBetween(from, to)
            .ContainingKeyword(request.Keyword)
            .WithLevel(request.Level, thresholds)
            .Select(p => new { p.CreatedAtUtc, p.Metrics.Likes, p.Metrics.Reposts, p.Metrics.Replies, p.Metrics.Quotes, p.Metrics.Views })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(p => DashboardRange.BucketStart(p.CreatedAtUtc, granularity, offset))
            .ToDictionary(
                g => g.Key,
                g => EngagementCalculator.Summarize(
                    g.Select(p => new EngagementMetrics(p.Likes, p.Reposts, p.Replies, p.Quotes, p.Views)),
                    thresholds));
    }

    /// <summary>Continuous series: empty buckets are emitted as zeros so charts do not hide quiet periods.</summary>
    private static List<SeriesPointDto> BuildSeries(
        Dictionary<DateTimeOffset, EngagementSummary> buckets,
        DateTimeOffset from,
        DateTimeOffset to,
        SeriesGranularity granularity,
        TimeSpan offset,
        EngagementType type)
    {
        var points = new List<(DateTimeOffset Bucket, EngagementSummary Summary)>();
        var step = DashboardRange.Step(granularity);

        for (var bucket = DashboardRange.BucketStart(from, granularity, offset); bucket <= to; bucket += step)
        {
            points.Add((bucket, buckets.GetValueOrDefault(bucket) ?? EngagementSummary.Empty));
        }

        var spikes = InsightMath.DetectSpikes([.. points.Select(p => p.Summary.Posts)]);

        return [.. points.Select((p, i) => new SeriesPointDto(
            p.Bucket,
            p.Summary.Posts,
            ValueOf(p.Summary, type),
            p.Summary.Interactions,
            p.Summary.Views,
            p.Summary.Rate,
            p.Summary.Likes,
            p.Summary.Reposts,
            p.Summary.Replies,
            p.Summary.Quotes,
            spikes[i]))];
    }

    private static TrendsDto BuildTrends(
        EngagementSummary current,
        EngagementSummary previous,
        DateTimeOffset previousFrom,
        DateTimeOffset previousTo,
        EngagementType type,
        bool comparable)
    {
        decimal? Change(long now, long before) => comparable ? InsightMath.PercentChange(now, before) : null;

        return new TrendsDto(
            previousFrom,
            previousTo,
            previous.Posts,
            comparable,
            Change(current.Posts, previous.Posts),
            Change(current.Interactions, previous.Interactions),
            Change(ValueOf(current, type), ValueOf(previous, type)),
            Change(current.Likes, previous.Likes),
            Change(current.Reposts, previous.Reposts),
            Change(current.Replies, previous.Replies),
            Change(current.Quotes, previous.Quotes),
            Change(current.Views, previous.Views),
            comparable && current.Rate is { } now && previous.Rate is { } before ? Math.Round(now - before, 2) : null);
    }

    private static long ValueOf(EngagementSummary summary, EngagementType type) => type switch
    {
        EngagementType.Likes => summary.Likes,
        EngagementType.Reposts => summary.Reposts,
        EngagementType.Replies => summary.Replies,
        EngagementType.Quotes => summary.Quotes,
        _ => summary.Interactions,
    };
}
