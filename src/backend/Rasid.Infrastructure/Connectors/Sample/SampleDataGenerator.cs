using System.Globalization;
using Rasid.Application.Connectors;
using Rasid.Domain.Engagement;

namespace Rasid.Infrastructure.Connectors.Sample;

/// <summary>
/// Deterministic generator of fictitious posts that behaves like a live feed: time is split into
/// 20-minute slots, each slot always yields the same posts (seeded by the slot number), new posts
/// appear as time passes, engagement grows with post age, and a small share of posts "disappears"
/// after a day so the deletion-sync path can be demonstrated.
/// </summary>
internal static class SampleDataGenerator
{
    /// <summary>
    /// The sample feed simulates an established topic with three weeks of history, so period-over-period
    /// trends can be demonstrated. (Live X Recent Search reaches back 7 days; history then builds up over time.)
    /// </summary>
    public const int WindowDays = 21;

    private const int SlotSeconds = 20 * 60;
    private const int MaxPostsPerSlot = 2;
    private const double DeletedShare = 0.03;
    private const double MissingViewsShare = 0.08;
    private const double ViralShare = 0.05;
    private static readonly TimeSpan GrowthTimeConstant = TimeSpan.FromHours(6);

    public static IEnumerable<GeneratedPost> PostsBetween(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        for (var slot = SlotOf(fromUtc); slot <= SlotOf(toUtc); slot++)
        {
            foreach (var post in PostsInSlot(slot))
            {
                if (post.CreatedAtUtc >= fromUtc && post.CreatedAtUtc <= toUtc)
                {
                    yield return post;
                }
            }
        }
    }

    /// <summary>Regenerates a post from its ID (the ID encodes slot and index).</summary>
    public static GeneratedPost? FindById(string postId)
    {
        if (postId.Length < 3
            || !long.TryParse(postId.AsSpan(0, postId.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var slot)
            || !int.TryParse(postId.AsSpan(postId.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            return null;
        }

        return PostsInSlot(slot).FirstOrDefault(p => p.Index == index);
    }

    public static SourceAuthor AuthorFor(GeneratedPost post) => new(
        AuthorId(post.AuthorIndex),
        SampleCatalog.Authors[post.AuthorIndex].Username,
        SampleCatalog.Authors[post.AuthorIndex].DisplayName,
        ProfileImageUrl: null,
        IsVerified: false);

    /// <summary>Engagement at a moment in time: grows towards the post's final potential as it ages.</summary>
    public static EngagementMetrics MetricsAt(GeneratedPost post, DateTimeOffset nowUtc)
    {
        var age = nowUtc - post.CreatedAtUtc;
        var growth = age <= TimeSpan.Zero ? 0d : 1d - Math.Exp(-age / GrowthTimeConstant);

        var views = (long)Math.Round(post.FinalViews * growth);
        var interactions = views * post.FinalRate;

        return new EngagementMetrics(
            likes: (long)Math.Round(interactions * 0.70),
            reposts: (long)Math.Round(interactions * 0.15),
            replies: (long)Math.Round(interactions * 0.10),
            quotes: (long)Math.Round(interactions * 0.05),
            views: post.HasViews ? views : null);
    }

    public static bool IsDeletedAt(GeneratedPost post, DateTimeOffset nowUtc) =>
        post.IsDeletedAfterADay && nowUtc - post.CreatedAtUtc > TimeSpan.FromDays(1);

    private static IEnumerable<GeneratedPost> PostsInSlot(long slot)
    {
        // System.Random with an explicit seed is deterministic across runs and machines.
        var random = new Random(unchecked((int)(slot * 2654435761L)));
        var count = random.Next(0, MaxPostsPerSlot + 1);
        var slotStart = DateTimeOffset.FromUnixTimeSeconds(slot * SlotSeconds);

        for (var index = 0; index < count; index++)
        {
            var textIndex = random.Next(SampleCatalog.Texts.Count);
            var isViral = random.NextDouble() < ViralShare;

            yield return new GeneratedPost(
                Id: string.Create(CultureInfo.InvariantCulture, $"{slot}{index:D2}"),
                Index: index,
                AuthorIndex: random.Next(SampleCatalog.Authors.Count),
                Text: SampleCatalog.Texts[textIndex].Text,
                Language: SampleCatalog.Texts[textIndex].Language,
                MediaFile: SampleCatalog.Texts[textIndex].MediaFile,
                MediaAlt: SampleCatalog.Texts[textIndex].MediaAlt,
                CreatedAtUtc: slotStart.AddSeconds(random.Next(SlotSeconds)),
                FinalViews: random.Next(800, 25_000) * (isViral ? 12 : 1),
                // Squared draw skews towards low rates: most posts get modest engagement, few get a lot.
                FinalRate: 0.002 + (Math.Pow(random.NextDouble(), 2) * 0.06),
                HasViews: random.NextDouble() >= MissingViewsShare,
                IsDeletedAfterADay: random.NextDouble() < DeletedShare);
        }
    }

    private static long SlotOf(DateTimeOffset instant) => instant.ToUnixTimeSeconds() / SlotSeconds;

    private static string AuthorId(int authorIndex) => string.Create(CultureInfo.InvariantCulture, $"5000{authorIndex:D2}");
}

internal sealed record GeneratedPost(
    string Id,
    int Index,
    int AuthorIndex,
    string Text,
    string Language,
    string? MediaFile,
    string? MediaAlt,
    DateTimeOffset CreatedAtUtc,
    long FinalViews,
    double FinalRate,
    bool HasViews,
    bool IsDeletedAfterADay);
