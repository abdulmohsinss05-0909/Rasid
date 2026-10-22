namespace Rasid.Application.Common.Abstractions;

/// <summary>
/// Cross-process mutual exclusion (several worker replicas share one database).
/// Throws <see cref="TimeoutException"/> when the lock cannot be obtained in time.
/// </summary>
public interface IDistributedLock
{
    Task<IDisposable> AcquireAsync(string resource, TimeSpan timeout, CancellationToken cancellationToken);
}

public static class LockNames
{
    /// <summary>
    /// Serialises writes of shared posts and authors. Different topics can match the same post, so
    /// parallel collection runs would otherwise race to insert the same rows.
    /// </summary>
    public const string PostWrites = "rasid:post-writes";
}
