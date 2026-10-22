using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rasid.Application.Alerts;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Results;
using Rasid.Application.Dashboard;
using Rasid.Application.Insights;
using Rasid.Application.Monitoring;
using Rasid.Application.Overview;
using Rasid.Application.Tests.Infrastructure;
using Rasid.Application.Topics;
using Rasid.Domain.Common;
using Rasid.Domain.Engagement;
using Rasid.Domain.Posts;
using Rasid.Domain.Topics;
using Rasid.Infrastructure.Persistence;

namespace Rasid.Application.Tests;

/// <summary>Insights, overview, alerts, media and the sample → live switch, against a real SQL Server.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class InsightsFlowTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Insights_rank_hashtags_rules_and_languages_from_the_filtered_posts()
    {
        var now = fixture.Clock.GetUtcNow();
        fixture.Source.AddPost("6001", now.AddHours(-3), new EngagementMetrics(10, 0, 0, 0, 100), "Convoy arrives #UAEAid #Relief");
        fixture.Source.AddPost("6002", now.AddHours(-2), new EngagementMetrics(20, 0, 0, 0, 100), "More aid #UAEAid");
        fixture.Source.AddPost("6003", now.AddHours(-1), new EngagementMetrics(5, 0, 0, 0, 100), "مساعدات #UAEAid", language: "ar");
        var topic = await CreateTopicAsync([new RuleInput(RuleType.Hashtag, "UAEAid"), new RuleInput(RuleType.Keyword, "convoy")]);
        await IngestAsync(topic.Id);

        var insights = (await SendAsync(new GetTopicInsightsQuery(topic.Id))).Value;

        Assert.Equal(3, insights.PostsAnalysed);
        Assert.Equal("UAEAid", insights.TopHashtags[0].Tag);
        Assert.Equal(3, insights.TopHashtags[0].Posts);
        Assert.Equal(100m, insights.TopHashtags[0].SharePercent);

        var hashtagRule = insights.Rules.Single(r => r.Type == RuleType.Hashtag);
        var keywordRule = insights.Rules.Single(r => r.Type == RuleType.Keyword);
        Assert.Equal(3, hashtagRule.Posts);
        Assert.Equal(1, keywordRule.Posts);

        Assert.Equal(["en", "ar"], insights.Languages.Select(l => l.Code));
        Assert.Equal("test_account", insights.TopAccounts.Single().Username);
        Assert.Equal(35, insights.TopAccounts.Single().Interactions);
        Assert.Equal(3, insights.Heatmap.Cells.Sum(day => day.Sum()));
        Assert.NotEmpty(insights.Highlights);
    }

    [Fact]
    public async Task Dashboard_reports_trends_against_the_previous_period()
    {
        fixture.Source.SearchWindow = TimeSpan.FromDays(21); // history reaches the previous 7-day period
        var now = fixture.Clock.GetUtcNow();
        fixture.Source.AddPost("7001", now.AddDays(-10), new EngagementMetrics(10, 0, 0, 0, 1_000)); // previous 7 days
        fixture.Source.AddPost("7002", now.AddDays(-2), new EngagementMetrics(10, 0, 0, 0, 1_000));  // current 7 days
        fixture.Source.AddPost("7003", now.AddDays(-1), new EngagementMetrics(20, 0, 0, 0, 1_000));
        var topic = await CreateTopicAsync([new RuleInput(RuleType.Keyword, "UAE aid")]);
        await IngestAsync(topic.Id);

        var dashboard = (await SendAsync(new GetTopicDashboardQuery(topic.Id))).Value;

        Assert.Equal(2, dashboard.Kpis.Posts);
        Assert.True(dashboard.Trends.IsComparable);
        Assert.Equal(1, dashboard.Trends.PreviousPosts);
        Assert.Equal(100m, dashboard.Trends.Posts);         // 1 → 2 posts
        Assert.Equal(200m, dashboard.Trends.Interactions);  // 10 → 30
        Assert.Equal(30, dashboard.Series.Sum(p => p.Likes)); // per-bucket breakdown adds up to the KPI
    }

    [Fact]
    public async Task Trends_are_not_reported_against_a_period_without_data_coverage()
    {
        // X Recent Search reaches 7 days back, so a new topic has no complete data for the previous 7 days.
        fixture.Source.AddPost("7101", fixture.Clock.GetUtcNow().AddDays(-1), new EngagementMetrics(10, 0, 0, 0, 1_000));
        var topic = await CreateTopicAsync([new RuleInput(RuleType.Keyword, "UAE aid")]);
        await IngestAsync(topic.Id);

        var trends = (await SendAsync(new GetTopicDashboardQuery(topic.Id))).Value.Trends;
        var overviewRow = (await SendAsync(new GetOverviewQuery())).Value.Topics.Single();

        Assert.False(trends.IsComparable);
        Assert.Null(trends.Posts);
        Assert.Null(trends.EngagementRatePoints);
        Assert.Null(overviewRow.PostsChange);
    }

    [Fact]
    public async Task Post_media_is_stored_and_returned_with_alt_text()
    {
        var media = PostMedia.Create("photo", "https://pbs.twimg.com/media/example.jpg", "A convoy of trucks");
        fixture.Source.AddPost("8001", fixture.Clock.GetUtcNow().AddHours(-1), new EngagementMetrics(1, 0, 0, 0, 10), media: media);
        var topic = await CreateTopicAsync([new RuleInput(RuleType.Keyword, "UAE aid")]);
        await IngestAsync(topic.Id);

        var post = (await SendAsync(new GetTopicPostsQuery(topic.Id))).Value.Items.Single();

        Assert.Equal(new MediaDto("photo", "https://pbs.twimg.com/media/example.jpg", "A convoy of trucks"), post.Media);
    }

    [Fact]
    public async Task Switching_to_live_x_removes_sample_data_and_restarts_cursors()
    {
        fixture.Source.Source = DataSource.Sample;
        fixture.Source.AddPost("149343200", fixture.Clock.GetUtcNow().AddHours(-1), new EngagementMetrics(5, 0, 0, 0, 50));
        var topic = await CreateTopicAsync([new RuleInput(RuleType.Keyword, "UAE aid")]);
        await IngestAsync(topic.Id);

        // Configuration change only: the resolver now returns the X connector.
        fixture.Source.Source = DataSource.XApi;
        await using var scope = fixture.Services.CreateAsyncScope();
        var removed = await scope.ServiceProvider.GetRequiredService<SourceTransitionService>().RemoveSampleDataWhenLiveAsync(CancellationToken.None);

        Assert.Equal(1, removed);
        var db = scope.ServiceProvider.GetRequiredService<RasidDbContext>();
        Assert.False(await db.Posts.AnyAsync());
        Assert.False(await db.TopicMetricsHourly.AnyAsync());
        var stored = await db.Topics.AsNoTracking().SingleAsync(t => t.Id == topic.Id);
        Assert.Null(stored.LastSeenPostId);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "sample.removed"));

        // Running again is a no-op.
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<SourceTransitionService>().RemoveSampleDataWhenLiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Overview_summarises_every_topic_with_daily_sparklines()
    {
        var now = fixture.Clock.GetUtcNow();
        fixture.Source.AddPost("9001", now.AddHours(-3), new EngagementMetrics(10, 2, 1, 0, 500));
        var topic = await CreateTopicAsync([new RuleInput(RuleType.Keyword, "UAE aid")]);
        await IngestAsync(topic.Id);

        var overview = (await SendAsync(new GetOverviewQuery())).Value;

        Assert.Equal(1, overview.Totals.Topics);
        Assert.Equal(1, overview.Totals.Posts);
        Assert.Equal(7, overview.Days.Count);
        var row = overview.Topics.Single();
        Assert.Equal(7, row.PostsByDay.Count);
        Assert.Equal(1, row.PostsByDay.Sum());
        Assert.Equal(13, row.Interactions);
    }

    [Fact]
    public async Task A_burst_of_posts_raises_an_activity_spike_alert_and_failures_raise_collection_alerts()
    {
        var now = fixture.Clock.GetUtcNow();
        var id = 10_000;
        for (var day = 6; day >= 1; day--)
        {
            fixture.Source.AddPost((id++).ToString(System.Globalization.CultureInfo.InvariantCulture), now.AddDays(-day), new EngagementMetrics(1, 0, 0, 0, 10));
        }

        for (var i = 0; i < 12; i++)
        {
            fixture.Source.AddPost((id++).ToString(System.Globalization.CultureInfo.InvariantCulture), now.AddHours(-2).AddMinutes(i), new EngagementMetrics(1, 0, 0, 0, 10));
        }

        var topic = await CreateTopicAsync([new RuleInput(RuleType.Keyword, "UAE aid")]);
        await IngestAsync(topic.Id);

        var alerts = (await SendAsync(new GetAlertsQuery())).Value;
        Assert.Contains(alerts, a => a.Kind == AlertKind.ActivitySpike && a.TopicId == topic.Id);

        fixture.Source.NextFailure = new Connectors.SourceFailure(Connectors.SourceFailureKind.Unauthorized, "denied");
        await IngestAsync(topic.Id);

        alerts = (await SendAsync(new GetAlertsQuery())).Value;
        Assert.Contains(alerts, a => a.Kind == AlertKind.CollectionProblem && a.Severity == AlertSeverity.Critical);
    }

    private async Task<TopicDto> CreateTopicAsync(IReadOnlyList<RuleInput> rules)
    {
        var result = await SendAsync(new CreateTopicCommand($"Topic {Guid.NewGuid():N}"[..20], null, TopicPriority.Normal, rules));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    private async Task IngestAsync(Guid topicId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TopicIngestionService>().IngestAsync(topicId, CancellationToken.None);
    }

    private async Task<Result<T>> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(request);
    }
}
