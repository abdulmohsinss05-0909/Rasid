using Hangfire;
using Hangfire.Storage;
using Rasid.Application.Common.Abstractions;

namespace Rasid.Infrastructure.Jobs;

/// <summary>
/// <see cref="IDistributedLock"/> backed by Hangfire's SQL Server lock, so no extra infrastructure is needed.
/// </summary>
internal sealed class HangfireDistributedLock(JobStorage storage) : IDistributedLock
{
    public Task<IDisposable> AcquireAsync(string resource, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var connection = storage.GetConnection();
        try
        {
            var handle = connection.AcquireDistributedLock(resource, timeout);
            return Task.FromResult<IDisposable>(new Releaser(handle, connection));
        }
        catch (DistributedLockTimeoutException ex)
        {
            connection.Dispose();
            throw new TimeoutException($"Could not acquire lock '{resource}' within {timeout}.", ex);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private sealed class Releaser(IDisposable handle, IStorageConnection connection) : IDisposable
    {
        public void Dispose()
        {
            handle.Dispose();
            connection.Dispose();
        }
    }
}
