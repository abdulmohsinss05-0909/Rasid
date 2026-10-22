using Rasid.Domain.Quota;
using Rasid.Domain.Topics;

namespace Rasid.Domain.Tests.Quota;

public sealed class QuotaPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly QuotaSettings Settings = new(monthlyPostReadBudget: 10_000);

    [Fact]
    public void Healthy_budget_allows_normal_cadence()
    {
        var decision = QuotaPolicy.Evaluate(UsageWith(postsRead: 1_000), Settings, TopicPriority.Normal, Now);

        Assert.Equal(QuotaMode.Normal, decision.Mode);
        Assert.Equal(Settings.MaxPagesPerRun, decision.MaxPages);
        Assert.Equal(10m, decision.BudgetUsedPercent);
    }

    [Theory]
    [InlineData(TopicPriority.High, 1)]
    [InlineData(TopicPriority.Normal, 2)]
    [InlineData(TopicPriority.Low, 4)]
    public void Above_throttle_threshold_lower_priorities_slow_down(TopicPriority priority, int expectedMultiplier)
    {
        var decision = QuotaPolicy.Evaluate(UsageWith(postsRead: 8_500), Settings, priority, Now);

        Assert.Equal(QuotaMode.Throttled, decision.Mode);
        Assert.Equal(expectedMultiplier, decision.IntervalMultiplier);
    }

    [Fact]
    public void Critical_budget_serves_only_high_priority_with_minimal_pages()
    {
        var usage = UsageWith(postsRead: 9_600);

        var high = QuotaPolicy.Evaluate(usage, Settings, TopicPriority.High, Now);
        var normal = QuotaPolicy.Evaluate(usage, Settings, TopicPriority.Normal, Now);

        Assert.Equal(QuotaMode.Critical, high.Mode);
        Assert.Equal(1, high.MaxPages);
        Assert.False(normal.IsAllowed);
    }

    [Fact]
    public void Exhausted_budget_denies_until_next_month()
    {
        var decision = QuotaPolicy.Evaluate(UsageWith(postsRead: 10_000), Settings, TopicPriority.High, Now);

        Assert.False(decision.IsAllowed);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), decision.RetryAfterUtc);
    }

    [Fact]
    public void Exhausted_rate_limit_window_denies_until_reset_regardless_of_budget()
    {
        var usage = ApiUsagePeriod.Start(Now);
        var reset = Now.AddMinutes(12);
        usage.RecordRateLimited(reset, Now);

        var decision = QuotaPolicy.Evaluate(usage, Settings, TopicPriority.High, Now);

        Assert.False(decision.IsAllowed);
        Assert.Equal(reset, decision.RetryAfterUtc);
    }

    [Fact]
    public void Rate_limit_window_in_the_past_no_longer_blocks()
    {
        var usage = ApiUsagePeriod.Start(Now);
        usage.RecordRateLimited(Now.AddMinutes(-1), Now);

        Assert.True(QuotaPolicy.Evaluate(usage, Settings, TopicPriority.Normal, Now).IsAllowed);
    }

    [Fact]
    public void Usage_from_a_previous_month_does_not_count()
    {
        var september = ApiUsagePeriod.Start(Now.AddMonths(-1));
        september.RecordUsage(10_000, 100, null, Now.AddMonths(-1));

        Assert.Equal(QuotaMode.Normal, QuotaPolicy.Evaluate(september, Settings, TopicPriority.Low, Now).Mode);
    }

    [Fact]
    public void Zero_budget_means_no_monthly_cap()
    {
        var decision = QuotaPolicy.Evaluate(UsageWith(postsRead: 1_000_000), new QuotaSettings(0), TopicPriority.Low, Now);

        Assert.Equal(QuotaMode.Normal, decision.Mode);
    }

    private static ApiUsagePeriod UsageWith(long postsRead)
    {
        var usage = ApiUsagePeriod.Start(Now);
        usage.RecordUsage((int)postsRead, 1, null, Now);
        return usage;
    }
}
