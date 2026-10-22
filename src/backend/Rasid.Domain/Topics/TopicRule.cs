using Rasid.Domain.Common;

namespace Rasid.Domain.Topics;

/// <summary>One matching rule of a topic: a keyword, hashtag or public account, included or excluded.</summary>
public sealed class TopicRule : Entity<Guid>
{
    private TopicRule()
    {
    }

    public Guid TopicId { get; private set; }

    public RuleType Type { get; private set; }

    /// <summary>Normalised value: hashtags without '#', accounts without '@'.</summary>
    public string Value { get; private set; } = string.Empty;

    public bool IsExclusion { get; private set; }

    public static TopicRule Create(RuleType type, string value, bool isExclusion = false) => new()
    {
        Id = Guid.CreateVersion7(),
        Type = type,
        Value = TopicRuleValue.Normalize(type, value),
        IsExclusion = isExclusion,
    };

    /// <summary>Two rules are equivalent when type, exclusion flag and value (case-insensitive) match.</summary>
    public bool IsEquivalentTo(TopicRule other) =>
        Type == other.Type
        && IsExclusion == other.IsExclusion
        && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>The user-facing form, e.g. "#UAEAid" or "@account".</summary>
    public string DisplayValue => Type switch
    {
        RuleType.Hashtag => $"#{Value}",
        RuleType.Account => $"@{Value}",
        _ => Value,
    };
}
