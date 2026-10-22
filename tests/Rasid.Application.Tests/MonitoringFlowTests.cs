using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Results;
using Rasid.Application.Connectors;
using Rasid.Application.Dashboard;
using Rasid.Application.Monitoring;
using Rasid.Application.Tests.Infrastructure;
using Rasid.Application.Topics;
using Rasid.Domain.Common;
using Rasid.Domain.Engagement;
using Rasid.Domain.Topics;
using Rasid.Infrastructure.Persistence;

namespace Rasid.Application.Tests;

/// <summary>End-to-end flows of the application layer against a real SQL Server.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class MonitoringFlowTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Invalid_rules_are_rejected_with_field_keys_the_ui_can_bind()
    {
        var result = await SendAsync(new CreateTopicCommand("Bad", null, TopicPriority.Normal, [new RuleInput(RuleType.Account, "bad-name")]));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("rules[0].value", result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Creating_a_topic_saves_it_audits_it_and_queues_the_first_collection()
    {
        var topic = await CreateTopicAsync("UAE aid");

        Assert.Contains(topic.Id, fixture.Jobs.Enqueued);
        Assert.Equal("\"UAE aid\" -is:retweet", topic.GeneratedQuery);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RasidDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == topic.Id.ToString() && a.Action == "topic.created"));
    }

    [Fact]
    public async Task Duplicate_topic_names_are_a_conflict()
    {
        await CreateTopicAsync("Same name");

        var duplicate = await SendAsync(new CreateTopicCommand("Same name", null, TopicPriority.Normal, [new RuleInput(RuleType.Hashtag, "x")]));

        Assert.Equal(ErrorType.Conflict, duplicate.Error!.Type);
    }

    [Fact]
    public async Task Ingestion_stores_posts_builds_aggregates_and_the_dashboard_agrees_with_the_post_list()
    {
        var now = fixture.Clock.GetUtcNow();
        fixture.Source.AddPost("1001", now.AddHours(-30), new EngagementMetrics(120, 30, 10, 5, 6_000));
        fixture.Source.AddPost("1002", now.AddHours(-5), new EngagementMetrics(8, 1, 0, 0, 2_000));
        fixture.Source.AddPost("1003", now.AddHours(-2), new EngagementMetrics(40, 15, 6, 2, null));
        var topic = await CreateTopicAsync("Ingestion");

        var status = await IngestAsync(topic.Id);

        Assert.Equal(RefreshStatus.Success, status);

        var dashboard = (await SendAsync(new GetTopicDashboardQuery(topic.Id))).Value;
        Assert.True(dashboard.Filter.UsesPreAggregatedData);
        Assert.Equal(3, dashboard.Kpis.Posts);
        Assert.Equal(237, dashboard.Kpis.Interactions);
        Assert.Equal(2.18m, dashboard.Kpis.EngagementRate); // (165 + 9) / 8,000 — post without views excluded
        Assert.Equal(2, dashboard.Kpis.PostsWithViews);
        Assert.Equal(new LevelDistributionDto(0, 1, 1, 1), dashboard.Levels);
        Assert.Equal(DataSource.XApi, dashboard.Freshness.Source);

        var posts = (await SendAsync(new GetTopicPostsQuery(topic.Id))).Value;
        Assert.Equal(dashboard.Kpis.Posts, posts.TotalCount);

        var filtered = (await SendAsync(new GetTopicDashboardQuery(topic.Id, Level: EngagementLevel.Low))).Value;
        Assert.False(filtered.Filter.UsesPreAggregatedData);
        Assert.Equal(1, filtered.Kpis.Posts);
    }

    [Fact]
    public async Task Second_run_only_reads_posts_newer_than_the_cursor()
    {
        var now = fixture.Clock.GetUtcNow();
        fixture.Source.AddPost("2001", now.AddHours(-3), new EngagementMetrics(1, 0, 0, 0, 100));
        var topic = await CreateTopicAsync("Incremental");
        await IngestAsync(topic.Id);

        fixture.Source.AddPost("2002", now.AddHours(-1), new EngagementMetrics(2, 0, 0, 0, 100));
        await IngestAsync(topic.Id);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RasidDbContext>();
        Assert.Equal(2, await db.TopicPosts.CountAsync(tp => tp.TopicId == topic.Id));
        Assert.Equal("2002", (await db.Topics.SingleAsync(t => t.Id == topic.Id)).LastSeenPostId);

        // The second run read exactly one post: 1 + 1 posts recorded against the monthly X budget.
        var usage = await db.ApiUsagePeriods.SingleAsync();
        Assert.Equal(2, usage.PostsRead);
    }

    [Fact]
    public async Task Topics_sharing_posts_can_be_collected_in_parallel_without_conflicts()
    {
        var now = fixture.Clock.GetUtcNow();
        for (var i = 0; i < 40; i++)
        {
            fixture.Source.AddPost((11_000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture), now.AddMinutes(-i * 7), new EngagementMetrics(i, 0, 0, 0, 100));
        }

        var first = await CreateTopicAsync("Parallel A");
        var second = await CreateTopicAsync("Parallel B");

        // Same posts and authors, written concurrently by two runs (as two worker threads would).
        var statuses = await Task.WhenAll(IngestAsync(first.Id), IngestAsync(second.Id));

        Assert.All(statuses, s => Assert.Equal(RefreshStatus.Success, s));
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RasidDbContext>();
        Assert.Equal(40, await db.Posts.CountAsync());
        Assert.Equal(40, await db.TopicPosts.CountAsync(tp => tp.TopicId == first.Id));
        Assert.Equal(40, await db.TopicPosts.CountAsync(tp => tp.TopicId == second.Id));
    }

    [Fact]
    public async Task Rate_limited_run_is_recorded_and_does_not_advance_the_cursor()
    {
        fixture.Source.AddPost("3001", fixture.Clock.GetUtcNow().AddHours(-1), new EngagementMetrics(1, 0, 0, 0, 10));
        var topic = await CreateTopicAsync("Rate limited");
        var reset = fixture.Clock.GetUtcNow().AddMinutes(12);
        fixture.Source.NextFailure = new SourceFailure(SourceFailureKind.RateLimited, "X API rate limit reached.", reset);

        var status = await IngestAsync(topic.Id);

        Assert.Equal(RefreshStatus.RateLimited, status);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RasidDbContext>();
        Assert.Null((await db.Topics.SingleAsync(t => t.Id == topic.Id)).LastSeenPostId);
        Assert.Equal(0, (await db.ApiUsagePeriods.SingleAsync()).RateLimitRemaining);

        // The quota policy now refuses calls until X's reset time.
        var decision = await scope.ServiceProvider.GetRequiredService<IQuotaManager>()
            .EvaluateAsync(DataSource.XApi, TopicPriority.High, CancellationToken.None);
        Assert.False(decision.IsAllowed);
        Assert.Equal(reset, decision.RetryAfterUtc);
    }

    [Fact]
    public async Task Exhausted_monthly_budget_defers_collection_without_calling_the_source()
    {
        var topic = await CreateTopicAsync("Budget");
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var quota = scope.ServiceProvider.GetRequiredService<IQuotaManager>();
            await quota.EvaluateAsync(DataSource.XApi, TopicPriority.Normal, CancellationToken.None);
            await quota.RecordUsageAsync(DataSource.XApi, 1_000, 10, null, CancellationToken.None);
        }

        var status = await IngestAsync(topic.Id);

        Assert.Equal(RefreshStatus.Deferred, status);
        Assert.Equal(0, fixture.Source.SearchCalls);
    }

    [Fact]
    public async Task Metric_refresh_updates_counters_and_removes_posts_deleted_at_the_source()
    {
        var now = fixture.Clock.GetUtcNow();
        fixture.Source.AddPost("4001", now.AddHours(-2), new EngagementMetrics(10, 0, 0, 0, 1_000));
        fixture.Source.AddPost("4002", now.AddHours(-2), new EngagementMetrics(5, 0, 0, 0, 1_000));
        var topic = await CreateTopicAsync("Refresh");
        await IngestAsync(topic.Id);

        fixture.Source.UpdateMetrics("4001", new EngagementMetrics(50, 5, 0, 0, 2_000));
        fixture.Source.Delete("4002");
        fixture.Clock.Advance(TimeSpan.FromMinutes(20)); // young posts are due every 15 minutes

        await using var scope = fixture.Services.CreateAsyncScope();
        var refreshed = await scope.ServiceProvider.GetRequiredService<MetricsRefreshService>().RefreshDueAsync(CancellationToken.None);

        Assert.Equal(1, refreshed);
        var db = scope.ServiceProvider.GetRequiredService<RasidDbContext>();
        var post = await db.Posts.AsNoTracking().SingleAsync();
        Assert.Equal("4001", post.Id);
        Assert.Equal(50, post.Metrics.Likes);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "post.removed_unavailable"));

        var dashboard = (await SendAsync(new GetTopicDashboardQuery(topic.Id))).Value;
        Assert.Equal(1, dashboard.Kpis.Posts);
        Assert.Equal(55, dashboard.Kpis.Interactions);
    }

    [Fact]
    public async Task Deleting_a_topic_removes_posts_no_other_topic_uses()
    {
        fixture.Source.AddPost("5001", fixture.Clock.GetUtcNow().AddHours(-1), new EngagementMetrics(1, 0, 0, 0, 10));
        var topic = await CreateTopicAsync("To delete");
        await IngestAsync(topic.Id);

        var result = await SendAsync(new DeleteTopicCommand(topic.Id));

        Assert.True(result.IsSuccess);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RasidDbContext>();
        Assert.False(await db.Posts.AnyAsync());
        Assert.False(await db.TopicMetricsHourly.AnyAsync());
    }

    private async Task<TopicDto> CreateTopicAsync(string keyword)
    {
        var result = await SendAsync(new CreateTopicCommand(keyword, null, TopicPriority.Normal, [new RuleInput(RuleType.Keyword, keyword)]));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    private async Task<RefreshStatus> IngestAsync(Guid topicId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TopicIngestionService>().IngestAsync(topicId, CancellationToken.None);
    }

    private async Task<Result<T>> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(request);
    }
}
