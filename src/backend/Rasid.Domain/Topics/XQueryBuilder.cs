using Rasid.Domain.Common;

namespace Rasid.Domain.Topics;

/// <summary>
/// Translates topic rules into an X API v2 search query.
/// Inclusions are OR-ed, exclusions are negated, and retweets are excluded to avoid double counting.
/// Rule values are whitelisted by <see cref="TopicRuleValue"/>, so no operator injection is possible.
/// </summary>
/// <example>("UAE aid" OR #UAEAid OR from:account) -"giveaway" -is:retweet</example>
public static class XQueryBuilder
{
    public const string ExcludeRetweetsOperator = "-is:retweet";

    public static string Build(IEnumerable<TopicRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var ruleList = rules as IReadOnlyCollection<TopicRule> ?? [.. rules];

        var inclusions = ruleList.Where(r => !r.IsExclusion).Select(ToFragment).ToList();
        if (inclusions.Count == 0)
        {
            throw new DomainException("A topic needs at least one keyword, hashtag or account to include.");
        }

        var parts = new List<string>
        {
            inclusions.Count == 1 ? inclusions[0] : $"({string.Join(" OR ", inclusions)})",
        };
        parts.AddRange(ruleList.Where(r => r.IsExclusion).Select(r => "-" + ToFragment(r)));
        parts.Add(ExcludeRetweetsOperator);

        return string.Join(' ', parts);
    }

    private static string ToFragment(TopicRule rule) => rule.Type switch
    {
        // Keywords are always quoted: exact phrase, and never interpreted as an operator such as OR.
        RuleType.Keyword => $"\"{rule.Value}\"",
        RuleType.Hashtag => $"#{rule.Value}",
        RuleType.Account => $"from:{rule.Value}",
        _ => throw new DomainException($"Unsupported rule type '{rule.Type}'."),
    };
}
