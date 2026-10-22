using Rasid.Domain.Common;
using Rasid.Domain.Topics;

namespace Rasid.Domain.Refresh;

/// <summary>Operational record of each collection run — the basis for status and error states in the UI.</summary>
public sealed class RefreshLog : Entity<long>
{
    public const int MessageMaxLength = 500;

    private RefreshLog()
    {
    }

    public Guid? TopicId { get; private set; }

    public RefreshJobType JobType { get; private set; }

    public DataSource? Source { get; private set; }

    public DateTimeOffset StartedUtc { get; private set; }

    public DateTimeOffset? FinishedUtc { get; private set; }

    public RefreshStatus Status { get; private set; }

    public int PostsProcessed { get; private set; }

    public int ApiCalls { get; private set; }

    public DateTimeOffset? RetryAfterUtc { get; private set; }

    public string? Message { get; private set; }

    public static RefreshLog Start(Guid? topicId, RefreshJobType jobType, DataSource? source, DateTimeOffset nowUtc) =>
        new()
        {
            TopicId = topicId,
            JobType = jobType,
            Source = source,
            StartedUtc = nowUtc,
            Status = RefreshStatus.Pending,
        };

    public void Complete(
        RefreshStatus status,
        DateTimeOffset nowUtc,
        int postsProcessed,
        int apiCalls,
        DateTimeOffset? retryAfterUtc = null,
        string? message = null)
    {
        Status = status;
        FinishedUtc = nowUtc;
        PostsProcessed = postsProcessed;
        ApiCalls = apiCalls;
        RetryAfterUtc = retryAfterUtc;
        Message = message is { Length: > MessageMaxLength } ? message[..MessageMaxLength] : message;
    }
}
