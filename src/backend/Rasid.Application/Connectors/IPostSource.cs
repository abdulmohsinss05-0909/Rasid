using Rasid.Domain.Common;

namespace Rasid.Application.Connectors;

/// <summary>
/// Port for any source of public posts (Ports &amp; Adapters). X API v2 is one adapter; the Sample
/// connector is another. Adding a platform means adding an adapter — the core does not change.
/// </summary>
public interface IPostSource
{
    DataSource Source { get; }

    /// <summary>Maximum number of IDs accepted by one <see cref="LookupAsync"/> call.</summary>
    int MaxLookupBatchSize { get; }

    /// <summary>Oldest incremental cursor the source accepts (null = no limit). X rejects a since_id outside its search window.</summary>
    TimeSpan? MaxCursorAge { get; }

    /// <summary>How far back a first search reaches (X Recent Search: 7 days). Bounds which periods have complete data.</summary>
    TimeSpan SearchWindow { get; }

    Task<SourceResult<SourcePage>> SearchRecentAsync(SourceSearchRequest request, CancellationToken cancellationToken);

    Task<SourceResult<SourceLookup>> LookupAsync(IReadOnlyCollection<string> postIds, CancellationToken cancellationToken);
}

/// <summary>Chooses the active source for this environment based on configuration.</summary>
public interface IPostSourceResolver
{
    /// <summary>The configured source, or null when none is configured (e.g. no X credentials and sample data disallowed).</summary>
    IPostSource? Resolve();
}
