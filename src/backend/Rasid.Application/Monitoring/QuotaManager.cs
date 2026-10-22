using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Options;
using Rasid.Domain.Common;
using Rasid.Domain.Quota;
using Rasid.Domain.Topics;

namespace Rasid.Application.Monitoring;

/// <summary>Tracks X API consumption and applies <see cref="QuotaPolicy"/>. Sample data never consumes quota.</summary>
public interface IQuotaManager
{
    /// <summary>
    /// Decides whether a call may be made now. Call this first in a unit of work: it may create the
    /// current month's usage row.
    /// </summary>
    Task<QuotaDecision> EvaluateAsync(DataSource source, TopicPriority priority, CancellationToken cancellationToken);

    Task RecordUsageAsync(DataSource source, int postsRead, int requests, RateLimitWindow? window, CancellationToken cancellationToken);

    Task RecordRateLimitedAsync(DataSource source, DateTimeOffset resetUtc, CancellationToken cancellationToken);

    Task<ApiUsagePeriod?> GetCurrentPeriodAsync(CancellationToken cancellationToken);
}

internal sealed class QuotaManager(IRasidDbContext db, IOptions<MonitoringOptions> options, TimeProvider clock) : IQuotaManager
{
    private static readonly QuotaDecision SampleDecision =
        new(QuotaMode.Normal, 1, 1, null, "Sample data does not consume X API quota.");

    public async Task<QuotaDecision> EvaluateAsync(DataSource source, TopicPriority priority, CancellationToken cancellationToken)
    {
        if (source == DataSource.Sample)
        {
            return SampleDecision with { MaxPages = options.Value.Quota.MaxPagesPerRun };
        }

        var now = clock.GetUtcNow();
        var usage = await EnsureCurrentPeriodAsync(now, cancellationToken);
        return QuotaPolicy.Evaluate(usage, options.Value.Quota.ToSettings(), priority, now);
    }

    public async Task RecordUsageAsync(
        DataSource source, int postsRead, int requests, RateLimitWindow? window, CancellationToken cancellationToken)
    {
        if (source == DataSource.Sample)
        {
            return;
        }

        var now = clock.GetUtcNow();

        if (await IncrementAsync(now, postsRead, requests, window, cancellationToken) == 0)
        {
            // The month rolled over during the run: create the new period, then record.
            await EnsureCurrentPeriodAsync(now, cancellationToken);
            await IncrementAsync(now, postsRead, requests, window, cancellationToken);
        }
    }

    public async Task RecordRateLimitedAsync(DataSource source, DateTimeOffset resetUtc, CancellationToken cancellationToken)
    {
        if (source == DataSource.Sample)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var key = ApiUsagePeriod.PeriodKey(now);
        await EnsureCurrentPeriodAsync(now, cancellationToken);

        await db.ApiUsagePeriods
            .Where(p => p.Id == key)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.RateLimitRemaining, 0)
                    .SetProperty(p => p.RateLimitResetUtc, resetUtc)
                    .SetProperty(p => p.UpdatedUtc, now),
                cancellationToken);
    }

    public Task<ApiUsagePeriod?> GetCurrentPeriodAsync(CancellationToken cancellationToken)
    {
        var key = ApiUsagePeriod.PeriodKey(clock.GetUtcNow());
        return db.ApiUsagePeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == key, cancellationToken);
    }

    /// <summary>Atomic increment in the database: safe with several worker replicas, no lost updates.</summary>
    private Task<int> IncrementAsync(
        DateTimeOffset now, int postsRead, int requests, RateLimitWindow? window, CancellationToken cancellationToken)
    {
        var key = ApiUsagePeriod.PeriodKey(now);
        return db.ApiUsagePeriods
            .Where(p => p.Id == key)
            .ExecuteUpdateAsync(
                setters =>
                {
                    setters.SetProperty(p => p.PostsRead, p => p.PostsRead + postsRead);
                    setters.SetProperty(p => p.Requests, p => p.Requests + requests);
                    setters.SetProperty(p => p.UpdatedUtc, now);
                    if (window is not null)
                    {
                        setters.SetProperty(p => p.RateLimitRemaining, window.Remaining);
                        setters.SetProperty(p => p.RateLimitResetUtc, window.ResetUtc);
                    }
                },
                cancellationToken);
    }

    private async Task<ApiUsagePeriod> EnsureCurrentPeriodAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var key = ApiUsagePeriod.PeriodKey(now);
        var existing = await db.ApiUsagePeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == key, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var period = ApiUsagePeriod.Start(now);
        db.ApiUsagePeriods.Add(period);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another replica created the row first; stop tracking ours and use theirs.
            db.ApiUsagePeriods.Remove(period);
        }

        return await db.ApiUsagePeriods.AsNoTracking().FirstAsync(p => p.Id == key, cancellationToken);
    }
}
