namespace Rasid.Application.Common.Abstractions;

/// <summary>
/// Records security-relevant actions. Entries join the current unit of work, so an action and its
/// audit record are committed together — or not at all.
/// </summary>
public interface IAuditTrail
{
    void Record(string action, string entityType, string? entityId, string? details = null);
}

public static class AuditActions
{
    public const string TopicCreated = "topic.created";
    public const string TopicUpdated = "topic.updated";
    public const string TopicPaused = "topic.paused";
    public const string TopicResumed = "topic.resumed";
    public const string TopicDeleted = "topic.deleted";
    public const string TopicRefreshRequested = "topic.refresh_requested";
    public const string PostRemovedUnavailable = "post.removed_unavailable";
    public const string ContentPurged = "content.purged";
    public const string SampleDataRemoved = "sample.removed";
}
