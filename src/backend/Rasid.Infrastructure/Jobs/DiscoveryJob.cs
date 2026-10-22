using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using Rasid.Application.Monitoring;

namespace Rasid.Infrastructure.Jobs;

/// <summary>
/// Runs discovery for one topic. A per-topic distributed lock guarantees that two workers never
/// collect the same topic at once — which would spend X API quota twice.
/// </summary>
[Queue(JobQueues.Discovery)]
public sealed partial class DiscoveryJob(
    TopicIngestionService ingestion,
    JobStorage storage,
    ILogger<DiscoveryJob> logger)
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(1);

    [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    [JobDisplayName("Discovery for topic {0}")]
    public async Task RunAsync(Guid topicId, CancellationToken cancellationToken)
    {
        using var connection = storage.GetConnection();

        IDisposable topicLock;
        try
        {
            topicLock = connection.AcquireDistributedLock($"rasid:discovery:{topicId:N}", LockTimeout);
        }
        catch (DistributedLockTimeoutException)
        {
            LogAlreadyRunning(topicId);
            return;
        }

        using (topicLock)
        {
            await ingestion.IngestAsync(topicId, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovery for topic {TopicId} is already running on another worker; skipped")]
    private partial void LogAlreadyRunning(Guid topicId);
}
