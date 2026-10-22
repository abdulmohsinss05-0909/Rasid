using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;
using Rasid.Application.Connectors;
using Rasid.Application.Dashboard;
using Rasid.Domain.Common;
using Rasid.Domain.Engagement;
using Rasid.Domain.Insights;
using Rasid.Domain.Topics;

namespace Rasid.Application.Overview;

/// <summary>Cross-topic summary for the home screen: last 7 days versus the 7 days before.</summary>
public sealed record GetOverviewQuery(int UtcOffsetMinutes = 0) : IQuery<OverviewDto>;

public sealed record OverviewDto(
    DateTimeOffset GeneratedUtc,
    DataSource? Source,
    string SourceLabel,
    DateTimeOffset? LastUpdatedUtc,
    OverviewTotalsDto Totals,
    IReadOnlyList<DateTimeOffset> Days,
    IReadOnlyList<TopicOverviewDto> Topics);

public sealed record OverviewTotalsDto(
    int Topics,
    int ActiveTopics,
    long Posts,
    long Interactions,
    long? Views,
    decimal? EngagementRate,
    decimal? PostsChange,
    decimal? InteractionsChange,
    decimal? ViewsChange,
    decimal? EngagementRatePoints);

public sealed record TopicOverviewDto(
    Guid Id,
    string Name,
    TopicPriority Priority,
    bool IsActive,
    RefreshStatus Status,
    bool IsStale,
    DateTimeOffset? LastSuccessfulRefreshUtc,
    long Posts,
    long Interactions,
    long? Views,
    decimal? EngagementRate,
    EngagementLevel EngagementLevel,
    decimal? PostsChange,
    decimal? InteractionsChange,
    IReadOnlyList<long> PostsByDay,
    IReadOnlyList<long> InteractionsByDay);

internal sealed class GetOverviewValidator : AbstractValidator<GetOverviewQuery>
{
    public GetOverviewValidator() =>
        RuleFor(q => q.UtcOffsetMinutes).InclusiveBetween(-DashboardRange.MaxUtcOffsetMinutes, DashboardRange.MaxUtcOffsetMinutes);
}

internal sealed class GetOverviewHandler(
    IRasidDbContext db,
    IPostSourceResolver sourceResolver,
    IOptions<MonitoringOptions> options,
    TimeProvider clock) : IRequestHandler<GetOverviewQuery, OverviewDto>
{
    private const int Days = 7;

    public async Task<Result<OverviewDto>> HandleAsync(GetOverviewQuery request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var thresholds = settings.Engagement.ToThresholds();
        var now = clock.GetUtcNow();
        var offset = TimeSpan.FromMinutes(request.UtcOffsetMinutes);

        var today = DashboardRange.BucketStart(now, SeriesGranularity.Day, offset);
        var currentFrom = today.AddDays(-(Days - 1));
        var previousFrom = currentFrom.AddDays(-Days);
        var days = Enumerable.Range(0, Days).Select(i => currentFrom.AddDays(i)).ToList();

        var topics = await db.Topics.AsNoTracking().OrderByDescending(t => t.IsActive).ThenBy(t => t.Name).ToListAsync(cancellationToken);
        var rows = await db.TopicMetricsHourly.AsNoTracking()
            .Where(h => h.HourUtc >= previousFrom)
            .ToListAsync(cancellationToken);

        var byTopic = rows.ToLookup(r => r.TopicId);
        var source = sourceResolver.Resolve();
        var allComparable = true;
        var allCurrent = EngagementSummary.Empty;
        var allPrevious = EngagementSummary.Empty;
        var topicDtos = new List<TopicOverviewDto>(topics.Count);

        foreach (var topic in topics)
        {
            var hours = byTopic[topic.Id].ToList();
            var current = Sum(hours.Where(h => h.HourUtc >= currentFrom));
            var previous = Sum(hours.Where(h => h.HourUtc < currentFrom));
            var comparable = DataCoverage.Covers(topic, source, previousFrom);
            allComparable &= comparable || !topic.IsActive;
            allCurrent = allCurrent.Add(current);
            allPrevious = allPrevious.Add(previous);

            var daily = hours
                .Where(h => h.HourUtc >= currentFrom)
                .GroupBy(h => DashboardRange.BucketStart(h.HourUtc, SeriesGranularity.Day, offset))
                .ToDictionary(g => g.Key, g => Sum(g));

            topicDtos.Add(new TopicOverviewDto(
                topic.Id,
                topic.Name,
                topic.Priority,
                topic.IsActive,
                topic.LastRefreshStatus,
                topic.IsStale(now, settings.RefreshInterval(topic.Priority)),
                topic.LastSuccessfulRefreshUtc,
                current.Posts,
                current.Interactions,
                current.ViewedPosts > 0 ? current.Views : null,
                current.Rate,
                EngagementCalculator.Level(current.Rate, thresholds),
                comparable ? InsightMath.PercentChange(current.Posts, previous.Posts) : null,
                comparable ? InsightMath.PercentChange(current.Interactions, previous.Interactions) : null,
                [.. days.Select(d => daily.GetValueOrDefault(d)?.Posts ?? 0)],
                [.. days.Select(d => daily.GetValueOrDefault(d)?.Interactions ?? 0)]));
        }

        return new OverviewDto(
            now,
            source?.Source,
            DataFreshnessBuilder.SourceLabel(source?.Source),
            topics.Max(t => t.LastSuccessfulRefreshUtc),
            new OverviewTotalsDto(
                topics.Count,
                topics.Count(t => t.IsActive),
                allCurrent.Posts,
                allCurrent.Interactions,
                allCurrent.ViewedPosts > 0 ? allCurrent.Views : null,
                allCurrent.Rate,
                allComparable ? InsightMath.PercentChange(allCurrent.Posts, allPrevious.Posts) : null,
                allComparable ? InsightMath.PercentChange(allCurrent.Interactions, allPrevious.Interactions) : null,
                allComparable ? InsightMath.PercentChange(allCurrent.Views, allPrevious.Views) : null,
                allComparable && allCurrent.Rate is { } c && allPrevious.Rate is { } p ? Math.Round(c - p, 2) : null),
            days,
            topicDtos);
    }

    private static EngagementSummary Sum(IEnumerable<TopicMetricsHourly> hours) =>
        hours.Aggregate(EngagementSummary.Empty, (sum, h) => sum.Add(h.ToSummary()));
}
