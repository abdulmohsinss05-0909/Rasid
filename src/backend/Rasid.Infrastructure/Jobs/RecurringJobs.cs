using Hangfire;
using Rasid.Application.Monitoring;

namespace Rasid.Infrastructure.Jobs;

public static class JobQueues
{
    public const string Discovery = "discovery";
    public const string Default = "default";

    public static string[] All { get; } = [Discovery, Default];
}

/// <summary>Every minute: queue discovery for each topic whose cadence (and quota) says it is due.</summary>
public sealed class DiscoverySchedulerJob(DiscoveryPlanner planner, SourceTransitionService transition, IBackgroundJobClient jobs)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // Switching to live X data is configuration only: sample data is cleared on the first live cycle.
        await transition.RemoveSampleDataWhenLiveAsync(cancellationToken);

        foreach (var topicId in await planner.FindDueTopicsAsync(cancellationToken))
        {
            jobs.Enqueue<DiscoveryJob>(job => job.RunAsync(topicId, CancellationToken.None));
        }
    }
}

/// <summary>Tiered metric refresh and deletion sync.</summary>
public sealed class MetricsRefreshJob(MetricsRefreshService service)
{
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 1)]
    public Task RunAsync(CancellationToken cancellationToken) => service.RefreshDueAsync(cancellationToken);
}

/// <summary>Daily retention enforcement.</summary>
public sealed class RetentionJob(RetentionService service)
{
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    [AutomaticRetry(Attempts = 3)]
    public Task RunAsync(CancellationToken cancellationToken) => service.PurgeAsync(cancellationToken);
}

public static class RecurringJobRegistration
{
    public const string DiscoveryScheduler = "discovery-scheduler";
    public const string MetricsRefresh = "metrics-refresh";
    public const string Retention = "retention";

    /// <summary>Registers (or updates) the recurring schedule. Called once by the Worker at startup.</summary>
    public static void RegisterRasidJobs(this IRecurringJobManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        manager.AddOrUpdate<DiscoverySchedulerJob>(DiscoveryScheduler, job => job.RunAsync(CancellationToken.None), Cron.Minutely());
        manager.AddOrUpdate<MetricsRefreshJob>(MetricsRefresh, job => job.RunAsync(CancellationToken.None), "*/5 * * * *");
        manager.AddOrUpdate<RetentionJob>(Retention, job => job.RunAsync(CancellationToken.None), Cron.Daily(2));
    }
}
