namespace Rasid.Domain.Engagement;

/// <summary>Which interaction an analyst wants to focus on when filtering and ranking.</summary>
public enum EngagementType
{
    All = 0,
    Likes = 1,
    Reposts = 2,
    Replies = 3,
    Quotes = 4,
}
