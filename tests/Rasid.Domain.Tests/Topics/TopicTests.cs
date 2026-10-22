using Rasid.Domain.Common;
using Rasid.Domain.Topics;

namespace Rasid.Domain.Tests.Topics;

public sealed class TopicTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_builds_query_and_starts_pending()
    {
        var topic = NewTopic(TopicRule.Create(RuleType.Hashtag, "UAEAid"));

        Assert.True(topic.IsActive);
        Assert.Equal(RefreshStatus.Pending, topic.LastRefreshStatus);
        Assert.Equal("#UAEAid -is:retweet", topic.GeneratedQuery);
    }

    [Fact]
    public void Duplicate_rules_are_collapsed()
    {
        var topic = NewTopic(
            TopicRule.Create(RuleType.Hashtag, "UAEAid"),
            TopicRule.Create(RuleType.Hashtag, "#uaeaid"));

        Assert.Single(topic.Rules);
    }

    [Fact]
    public void Changing_rules_resets_the_incremental_cursor()
    {
        var topic = NewTopic(TopicRule.Create(RuleType.Hashtag, "UAEAid"));
        topic.RecordRefresh(RefreshStatus.Success, Now, newestPostId: "1850000000000000001");

        topic.Update("Topic", null, TopicPriority.Normal, [TopicRule.Create(RuleType.Hashtag, "Relief")], Now);

        Assert.Null(topic.LastSeenPostId);
    }

    [Fact]
    public void RecordRefresh_only_moves_cursor_forward()
    {
        var topic = NewTopic(TopicRule.Create(RuleType.Hashtag, "UAEAid"));

        topic.RecordRefresh(RefreshStatus.Success, Now, "1850000000000000005");
        topic.RecordRefresh(RefreshStatus.Success, Now, "1850000000000000002");

        Assert.Equal("1850000000000000005", topic.LastSeenPostId);
    }

    [Fact]
    public void Failed_refresh_does_not_update_last_successful_time()
    {
        var topic = NewTopic(TopicRule.Create(RuleType.Hashtag, "UAEAid"));
        topic.RecordRefresh(RefreshStatus.Success, Now);

        topic.RecordRefresh(RefreshStatus.ConnectionFailed, Now.AddMinutes(15));

        Assert.Equal(Now, topic.LastSuccessfulRefreshUtc);
        Assert.Equal(RefreshStatus.ConnectionFailed, topic.LastRefreshStatus);
    }

    [Fact]
    public void Data_is_stale_after_two_missed_intervals()
    {
        var topic = NewTopic(TopicRule.Create(RuleType.Hashtag, "UAEAid"));
        topic.RecordRefresh(RefreshStatus.Success, Now);
        var interval = TimeSpan.FromMinutes(15);

        Assert.False(topic.IsStale(Now.AddMinutes(30), interval));
        Assert.True(topic.IsStale(Now.AddMinutes(31), interval));
    }

    [Fact]
    public void Paused_topic_is_never_due()
    {
        var topic = NewTopic(TopicRule.Create(RuleType.Hashtag, "UAEAid"));
        topic.Pause(Now);

        Assert.False(topic.IsDueForRefresh(Now.AddDays(1), TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Manual_refresh_is_throttled_from_the_last_request_even_before_the_job_runs()
    {
        var topic = NewTopic(TopicRule.Create(RuleType.Hashtag, "UAEAid"));
        var cooldown = TimeSpan.FromSeconds(60);

        topic.RequestManualRefresh(Now, cooldown);

        Assert.Equal(TimeSpan.FromSeconds(55), topic.ManualRefreshWait(Now.AddSeconds(5), cooldown));
        Assert.Throws<DomainException>(() => topic.RequestManualRefresh(Now.AddSeconds(5), cooldown));
        Assert.Equal(TimeSpan.Zero, topic.ManualRefreshWait(Now.AddSeconds(60), cooldown));
    }

    [Fact]
    public void Cursor_is_never_sent_to_a_different_source()
    {
        var topic = NewTopic(TopicRule.Create(RuleType.Hashtag, "UAEAid"));
        topic.RecordRefresh(RefreshStatus.Success, Now, "149343200", DataSource.Sample);

        Assert.Equal("149343200", topic.IncrementalCursor(DataSource.Sample, Now, null));
        Assert.Null(topic.IncrementalCursor(DataSource.XApi, Now, null));

        // First live run starts a fresh cursor even though the sample ID compares differently.
        topic.RecordRefresh(RefreshStatus.Success, Now, "1850000000000000001", DataSource.XApi);
        Assert.Equal("1850000000000000001", topic.IncrementalCursor(DataSource.XApi, Now, null));
    }

    [Fact]
    public void X_cursor_older_than_the_search_window_is_dropped()
    {
        var topic = NewTopic(TopicRule.Create(RuleType.Hashtag, "UAEAid"));
        var created = PostId.SnowflakeTime("1850000000000000001")!.Value;
        topic.RecordRefresh(RefreshStatus.Success, Now, "1850000000000000001", DataSource.XApi);

        Assert.NotNull(topic.IncrementalCursor(DataSource.XApi, created.AddDays(5), TimeSpan.FromDays(6)));
        Assert.Null(topic.IncrementalCursor(DataSource.XApi, created.AddDays(7), TimeSpan.FromDays(6)));
    }

    [Fact]
    public void Name_is_required()
    {
        Assert.Throws<DomainException>(() =>
            Topic.Create(" ", null, TopicPriority.Normal, [TopicRule.Create(RuleType.Hashtag, "x")], "tester", Now));
    }

    [Fact]
    public void Rule_count_is_limited()
    {
        var rules = Enumerable.Range(0, Topic.MaxRules + 1)
            .Select(i => TopicRule.Create(RuleType.Hashtag, $"tag{i}"))
            .ToList();

        Assert.Throws<DomainException>(() => Topic.Create("T", null, TopicPriority.Normal, rules, "tester", Now));
    }

    private static Topic NewTopic(params TopicRule[] rules) =>
        Topic.Create("Topic", null, TopicPriority.Normal, rules, "tester", Now);
}
