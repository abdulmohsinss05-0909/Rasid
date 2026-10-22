namespace Rasid.Domain.Posts;

/// <summary>Links a post to every topic it matched.</summary>
public sealed class TopicPost
{
    private TopicPost()
    {
    }

    public Guid TopicId { get; private set; }

    public string PostId { get; private set; } = string.Empty;

    public Post? Post { get; private set; }

    public DateTimeOffset MatchedUtc { get; private set; }

    public static TopicPost Link(Guid topicId, string postId, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postId);
        return new TopicPost { TopicId = topicId, PostId = postId, MatchedUtc = nowUtc };
    }
}
