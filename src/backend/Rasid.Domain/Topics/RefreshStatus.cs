namespace Rasid.Domain.Topics;

/// <summary>Outcome of the most recent attempt to collect data for a topic.</summary>
public enum RefreshStatus
{
    /// <summary>Topic created; first collection not yet completed.</summary>
    Pending = 0,
    Success = 1,
    NoResults = 2,

    /// <summary>Skipped by the Quota Manager to protect the API budget.</summary>
    Deferred = 3,
    RateLimited = 4,
    AuthFailed = 5,
    ConnectionFailed = 6,

    /// <summary>No data source is configured for this environment.</summary>
    NotConfigured = 7,
}
