using Hangfire;
using Rasid.Application.Common.Abstractions;

namespace Rasid.Infrastructure.Jobs;

/// <summary>Adapter for <see cref="IJobScheduler"/>: the API only enqueues; the Worker executes.</summary>
internal sealed class HangfireJobScheduler(IBackgroundJobClient jobs) : IJobScheduler
{
    public string EnqueueTopicDiscovery(Guid topicId) =>
        jobs.Enqueue<DiscoveryJob>(job => job.RunAsync(topicId, CancellationToken.None));
}
