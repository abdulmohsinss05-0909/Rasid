using Rasid.Domain.Common;
using Rasid.Domain.Topics;

namespace Rasid.Domain.Tests.Topics;

public sealed class XQueryBuilderTests
{
    [Fact]
    public void Inclusions_are_ored_exclusions_negated_and_retweets_excluded()
    {
        var query = XQueryBuilder.Build(
        [
            TopicRule.Create(RuleType.Keyword, "UAE aid"),
            TopicRule.Create(RuleType.Hashtag, "#UAEAid"),
            TopicRule.Create(RuleType.Account, "@some_account"),
            TopicRule.Create(RuleType.Keyword, "giveaway", isExclusion: true),
        ]);

        Assert.Equal("(\"UAE aid\" OR #UAEAid OR from:some_account) -\"giveaway\" -is:retweet", query);
    }

    [Fact]
    public void Single_inclusion_is_not_wrapped_in_parentheses()
    {
        var query = XQueryBuilder.Build([TopicRule.Create(RuleType.Hashtag, "UAEAid")]);

        Assert.Equal("#UAEAid -is:retweet", query);
    }

    [Fact]
    public void Keywords_are_always_quoted_so_they_cannot_act_as_operators()
    {
        var query = XQueryBuilder.Build([TopicRule.Create(RuleType.Keyword, "OR")]);

        Assert.StartsWith("\"OR\"", query, StringComparison.Ordinal);
    }

    [Fact]
    public void At_least_one_inclusion_is_required()
    {
        Assert.Throws<DomainException>(() =>
            XQueryBuilder.Build([TopicRule.Create(RuleType.Keyword, "spam", isExclusion: true)]));
    }
}
