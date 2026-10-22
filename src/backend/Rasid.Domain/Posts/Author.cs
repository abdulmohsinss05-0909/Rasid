using Rasid.Domain.Common;

namespace Rasid.Domain.Posts;

/// <summary>
/// Public author details — only what X's display requirements need (name, @username, avatar, verified).
/// No follower graphs or profiling data are stored.
/// </summary>
public sealed class Author : Entity<string>
{
    private Author()
    {
    }

    public string Username { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? ProfileImageUrl { get; private set; }

    public bool IsVerified { get; private set; }

    public DateTimeOffset UpdatedUtc { get; private set; }

    public static Author Create(
        string id, string username, string displayName, string? profileImageUrl, bool isVerified, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var author = new Author { Id = id };
        author.Update(username, displayName, profileImageUrl, isVerified, nowUtc);
        return author;
    }

    public void Update(string username, string displayName, string? profileImageUrl, bool isVerified, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        Username = username;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName;
        ProfileImageUrl = profileImageUrl;
        IsVerified = isVerified;
        UpdatedUtc = nowUtc;
    }
}
