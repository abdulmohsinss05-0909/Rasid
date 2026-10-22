using Rasid.Domain.Topics;

namespace Rasid.Domain.Tests.Topics;

public sealed class TopicRuleValueTests
{
    [Theory]
    [InlineData(RuleType.Hashtag, "#UAEAid", "UAEAid")]
    [InlineData(RuleType.Account, "@Some_Account", "Some_Account")]
    [InlineData(RuleType.Keyword, "  humanitarian    aid ", "humanitarian aid")]
    [InlineData(RuleType.Keyword, "مساعدات إنسانية", "مساعدات إنسانية")]
    [InlineData(RuleType.Hashtag, "#الإمارات", "الإمارات")]
    public void Valid_values_are_normalised(RuleType type, string raw, string expected)
    {
        Assert.True(TopicRuleValue.TryNormalize(type, raw, out var normalized, out _));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(RuleType.Keyword, "a")] // too short
    [InlineData(RuleType.Keyword, "aid\" OR from:x")] // operator injection attempt
    [InlineData(RuleType.Keyword, "(aid)")]
    [InlineData(RuleType.Keyword, "-aid")] // leading operator
    [InlineData(RuleType.Hashtag, "two words")]
    [InlineData(RuleType.Account, "this_name_is_too_long")]
    [InlineData(RuleType.Account, "bad-name")]
    [InlineData(RuleType.Keyword, "")]
    public void Invalid_values_are_rejected_with_a_message(RuleType type, string raw)
    {
        Assert.False(TopicRuleValue.TryNormalize(type, raw, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
