using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Options;
using Rasid.Application.Connectors;
using Rasid.Domain.Common;
using Rasid.Domain.Engagement;
using Rasid.Domain.Posts;

namespace Rasid.Application.Monitoring;

/// <summary>
/// Persists posts and authors received from a source: inserts new ones, refreshes existing ones and
/// appends a metric snapshot. Shared by discovery and metric refresh so both behave identically.
/// </summary>
public sealed class PostWriter(IRasidDbContext db, IOptions<MonitoringOptions> options)
{
    private const string UnknownAuthorName = "Unknown author";

    /// <summary>Upserts the posts (and their authors) and returns the tracked post entities.</summary>
    public async Task<IReadOnlyList<Post>> UpsertAsync(
        IReadOnlyList<SourcePost> posts,
        IReadOnlyList<SourceAuthor> authors,
        DataSource source,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (posts.Count == 0)
        {
            return [];
        }

        await UpsertAuthorsAsync(posts, authors, nowUtc, cancellationToken);

        var ids = posts.Select(p => p.Id).Distinct(StringComparer.Ordinal).ToList();
        var existing = await db.Posts
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, StringComparer.Ordinal, cancellationToken);

        var result = new List<Post>(posts.Count);
        foreach (var incoming in posts.DistinctBy(p => p.Id, StringComparer.Ordinal))
        {
            if (existing.TryGetValue(incoming.Id, out var post))
            {
                post.UpdateMetrics(incoming.Metrics, nowUtc, incoming.Media);
            }
            else
            {
                post = Post.Create(
                    incoming.Id,
                    incoming.AuthorId,
                    incoming.Text,
                    incoming.Language,
                    incoming.CreatedAtUtc,
                    incoming.Url,
                    source,
                    incoming.Metrics,
                    nowUtc,
                    options.Value.ContentRetention,
                    incoming.Media);
                db.Posts.Add(post);
            }

            db.MetricSnapshots.Add(MetricSnapshot.Capture(post.Id, incoming.Metrics, nowUtc));
            result.Add(post);
        }

        return result;
    }

    private async Task UpsertAuthorsAsync(
        IReadOnlyList<SourcePost> posts,
        IReadOnlyList<SourceAuthor> authors,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var requiredIds = posts.Select(p => p.AuthorId)
            .Concat(authors.Select(a => a.Id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var existing = await db.Authors
            .Where(a => requiredIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, StringComparer.Ordinal, cancellationToken);

        var incoming = authors.DistinctBy(a => a.Id, StringComparer.Ordinal).ToDictionary(a => a.Id, StringComparer.Ordinal);

        foreach (var id in requiredIds)
        {
            incoming.TryGetValue(id, out var details);

            if (existing.TryGetValue(id, out var author))
            {
                if (details is not null)
                {
                    author.Update(details.Username, details.DisplayName, details.ProfileImageUrl, details.IsVerified, nowUtc);
                }

                continue;
            }

            // A post must always reference an author row; if the source omitted the expansion, keep a placeholder.
            db.Authors.Add(details is null
                ? Author.Create(id, id, UnknownAuthorName, null, false, nowUtc)
                : Author.Create(id, details.Username, details.DisplayName, details.ProfileImageUrl, details.IsVerified, nowUtc));
        }
    }
}
