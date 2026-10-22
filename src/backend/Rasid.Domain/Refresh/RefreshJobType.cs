namespace Rasid.Domain.Refresh;

public enum RefreshJobType
{
    /// <summary>Search for new posts matching a topic.</summary>
    Discovery = 1,

    /// <summary>Refresh metrics of already collected posts (and detect deletions).</summary>
    MetricsRefresh = 2,

    /// <summary>Purge expired content.</summary>
    Retention = 3,
}
