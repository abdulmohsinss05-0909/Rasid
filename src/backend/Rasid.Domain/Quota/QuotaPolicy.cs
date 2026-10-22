using Rasid.Domain.Topics;

namespace Rasid.Domain.Quota;

/// <summary>
/// Decides whether a topic may call the X API now. Pure function of usage, settings, priority and time,
/// so it is fully unit-testable. See docs/03-flowcharts.md §5.
/// </summary>
public static class QuotaPolicy
{
    public static QuotaDecision Evaluate(
        ApiUsagePeriod? usage, QuotaSettings settings, TopicPriority priority, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // 1. Rate-limit window reported by X: never call before the reset time.
        if (usage is { RateLimitRemaining: <= 0, RateLimitResetUtc: { } resetUtc } && resetUtc > nowUtc)
        {
            return Denied(resetUtc, "X rate-limit window exhausted; waiting for reset.", null);
        }

        // 2. No monthly cap configured: only the rate-limit window applies.
        if (settings.MonthlyPostReadBudget == 0)
        {
            return new QuotaDecision(QuotaMode.Normal, settings.MaxPagesPerRun, 1, null, "No monthly cap configured.");
        }

        var postsRead = usage is not null && usage.Id == ApiUsagePeriod.PeriodKey(nowUtc) ? usage.PostsRead : 0;
        var usedPercent = Math.Round(postsRead * 100m / settings.MonthlyPostReadBudget, 2);

        if (usedPercent >= 100m)
        {
            return Denied(StartOfNextMonth(nowUtc), "Monthly X read budget exhausted.", usedPercent);
        }

        if (usedPercent >= settings.CriticalAtPercent)
        {
            return priority == TopicPriority.High
                ? new QuotaDecision(QuotaMode.Critical, 1, 1, null, "Budget critical: high-priority topics only, minimal pages.")
                {
                    BudgetUsedPercent = usedPercent,
                }
                : Denied(StartOfNextMonth(nowUtc), "Budget critical: protecting remaining budget for high-priority topics.", usedPercent);
        }

        if (usedPercent >= settings.ThrottleAtPercent)
        {
            var multiplier = priority switch
            {
                TopicPriority.High => 1,
                TopicPriority.Normal => 2,
                _ => 4,
            };
            return new QuotaDecision(QuotaMode.Throttled, settings.MaxPagesPerRun, multiplier, null, "Budget above throttle threshold: reduced cadence.")
            {
                BudgetUsedPercent = usedPercent,
            };
        }

        return new QuotaDecision(QuotaMode.Normal, settings.MaxPagesPerRun, 1, null, "Budget healthy.")
        {
            BudgetUsedPercent = usedPercent,
        };
    }

    private static QuotaDecision Denied(DateTimeOffset retryAfterUtc, string reason, decimal? usedPercent) =>
        new(QuotaMode.Denied, 0, 1, retryAfterUtc, reason) { BudgetUsedPercent = usedPercent };

    private static DateTimeOffset StartOfNextMonth(DateTimeOffset nowUtc)
    {
        var utc = nowUtc.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
    }
}
