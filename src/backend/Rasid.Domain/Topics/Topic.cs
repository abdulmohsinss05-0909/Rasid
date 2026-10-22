using Rasid.Domain.Common;

namespace Rasid.Domain.Topics;

/// <summary>
/// A monitoring topic: what to look for on X, and the state of its data collection.
/// Aggregate root for its <see cref="TopicRule"/>s.
/// </summary>
public sealed class Topic : Entity<Guid>
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int MaxRules = 25;

    private readonly List<TopicRule> _rules = [];

    private Topic()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public TopicPriority Priority { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>The X search query derived from the rules. Kept in sync on every rule change.</summary>
    public string GeneratedQuery { get; private set; } = string.Empty;

    /// <summary>Newest post already collected — used as <c>since_id</c> for incremental collection.</summary>
    public string? LastSeenPostId { get; private set; }

    /// <summary>The data source the cursor belongs to; a cursor is never sent to a different source.</summary>
    public DataSource? CursorSource { get; private set; }

    public DateTimeOffset? LastRefreshAttemptUtc { get; private set; }

    /// <summary>Last manual "refresh now" request — throttled so repeated clicks cannot spend API quota.</summary>
    public DateTimeOffset? LastRefreshRequestedUtc { get; private set; }

    public DateTimeOffset? LastSuccessfulRefreshUtc { get; private set; }

    public RefreshStatus LastRefreshStatus { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedUtc { get; private set; }

    /// <summary>Optimistic-concurrency token: prevents lost updates between concurrent editors.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<TopicRule> Rules => _rules.AsReadOnly();

    public static Topic Create(
        string name,
        string? description,
        TopicPriority priority,
        IReadOnlyCollection<TopicRule> rules,
        string createdBy,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        var topic = new Topic
        {
            Id = Guid.CreateVersion7(nowUtc),
            IsActive = true,
            LastRefreshStatus = RefreshStatus.Pending,
            CreatedUtc = nowUtc,
            CreatedBy = createdBy,
        };
        topic.SetDetails(name, description, priority);
        topic.ReplaceRules(rules);
        return topic;
    }

    public void Update(
        string name,
        string? description,
        TopicPriority priority,
        IReadOnlyCollection<TopicRule> rules,
        DateTimeOffset nowUtc)
    {
        SetDetails(name, description, priority);
        ReplaceRules(rules);
        UpdatedUtc = nowUtc;
    }

    public void Pause(DateTimeOffset nowUtc)
    {
        IsActive = false;
        UpdatedUtc = nowUtc;
    }

    public void Resume(DateTimeOffset nowUtc)
    {
        IsActive = true;
        UpdatedUtc = nowUtc;
    }

    /// <summary>Records the outcome of a collection run and advances the incremental cursor.</summary>
    public void RecordRefresh(RefreshStatus status, DateTimeOffset nowUtc, string? newestPostId = null, DataSource? source = null)
    {
        LastRefreshAttemptUtc = nowUtc;
        LastRefreshStatus = status;

        if (status is RefreshStatus.Success or RefreshStatus.NoResults)
        {
            LastSuccessfulRefreshUtc = nowUtc;
        }

        if (newestPostId is null)
        {
            return;
        }

        // A cursor from another source is meaningless here: start a new one.
        if (source is not null && source != CursorSource)
        {
            LastSeenPostId = null;
            CursorSource = source;
        }

        if (PostId.IsNewer(newestPostId, LastSeenPostId))
        {
            LastSeenPostId = newestPostId;
        }
    }

    /// <summary>
    /// The <c>since_id</c> to send to <paramref name="source"/>, or null for a full window. Dropped when it
    /// belongs to another source, or when it is older than <paramref name="maxAge"/> (X rejects a since_id
    /// outside its 7-day Recent Search window).
    /// </summary>
    public string? IncrementalCursor(DataSource source, DateTimeOffset nowUtc, TimeSpan? maxAge)
    {
        if (LastSeenPostId is null || CursorSource != source)
        {
            return null;
        }

        if (maxAge is { } limit && PostId.SnowflakeTime(LastSeenPostId) is { } created && nowUtc - created > limit)
        {
            return null;
        }

        return LastSeenPostId;
    }

    /// <summary>Forgets the incremental cursor (e.g. after data from another source was removed).</summary>
    public void ResetCursor()
    {
        LastSeenPostId = null;
        CursorSource = null;
    }

    /// <summary>
    /// Time until a manual refresh may be requested again (zero when allowed). The cooldown counts from
    /// the later of the last request and the last actual run.
    /// </summary>
    public TimeSpan ManualRefreshWait(DateTimeOffset nowUtc, TimeSpan cooldown)
    {
        var last = new[] { LastRefreshRequestedUtc, LastRefreshAttemptUtc }.Max();
        var wait = last is null ? TimeSpan.Zero : last.Value + cooldown - nowUtc;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    public void RequestManualRefresh(DateTimeOffset nowUtc, TimeSpan cooldown)
    {
        if (!IsActive)
        {
            throw new DomainException("A paused topic cannot be refreshed.");
        }

        if (ManualRefreshWait(nowUtc, cooldown) > TimeSpan.Zero)
        {
            throw new DomainException("A refresh was requested too recently.");
        }

        LastRefreshRequestedUtc = nowUtc;
    }

    public bool IsDueForRefresh(DateTimeOffset nowUtc, TimeSpan interval) =>
        IsActive && (LastRefreshAttemptUtc is null || nowUtc - LastRefreshAttemptUtc.Value >= interval);

    /// <summary>Data is stale when the last success is older than two refresh intervals.</summary>
    public bool IsStale(DateTimeOffset nowUtc, TimeSpan interval) =>
        LastSuccessfulRefreshUtc is not null && nowUtc - LastSuccessfulRefreshUtc.Value > interval * 2;

    private void SetDetails(string name, string? description, TopicPriority priority)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            throw new DomainException($"Topic name is required and must not exceed {NameMaxLength} characters.");
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription?.Length > DescriptionMaxLength)
        {
            throw new DomainException($"Description must not exceed {DescriptionMaxLength} characters.");
        }

        if (!Enum.IsDefined(priority))
        {
            throw new DomainException("Unknown priority.");
        }

        Name = trimmedName;
        Description = trimmedDescription;
        Priority = priority;
    }

    private void ReplaceRules(IReadOnlyCollection<TopicRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var distinct = new List<TopicRule>(rules.Count);
        foreach (var rule in rules)
        {
            if (!distinct.Exists(existing => existing.IsEquivalentTo(rule)))
            {
                distinct.Add(rule);
            }
        }

        if (distinct.Count > MaxRules)
        {
            throw new DomainException($"A topic can have at most {MaxRules} rules.");
        }

        var query = XQueryBuilder.Build(distinct);

        // A different query matches a different set of posts, so the incremental cursor restarts.
        if (!string.Equals(query, GeneratedQuery, StringComparison.Ordinal))
        {
            LastSeenPostId = null;
        }

        _rules.Clear();
        _rules.AddRange(distinct);
        GeneratedQuery = query;
    }
}
