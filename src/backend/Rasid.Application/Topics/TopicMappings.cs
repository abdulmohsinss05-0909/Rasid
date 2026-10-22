using Rasid.Application.Common.Options;
using Rasid.Domain.Topics;

namespace Rasid.Application.Topics;

/// <summary>Explicit, reviewable mapping (no reflection-based mapper — see ADR-010).</summary>
internal static class TopicMappings
{
    public static TopicDto ToDto(this Topic topic, long postCount, MonitoringOptions options, DateTimeOffset nowUtc)
    {
        var interval = options.RefreshInterval(topic.Priority);

        return new TopicDto(
            topic.Id,
            topic.Name,
            topic.Description,
            topic.Priority,
            topic.IsActive,
            [.. topic.Rules
                .OrderBy(r => r.IsExclusion)
                .ThenBy(r => r.Type)
                .Select(r => new RuleDto(r.Id, r.Type, r.Value, r.DisplayValue, r.IsExclusion))],
            topic.GeneratedQuery,
            postCount,
            topic.LastRefreshStatus,
            topic.LastRefreshAttemptUtc,
            topic.LastSuccessfulRefreshUtc,
            topic.IsStale(nowUtc, interval),
            (int)interval.TotalMinutes,
            topic.CreatedUtc,
            topic.CreatedBy,
            topic.UpdatedUtc,
            Convert.ToBase64String(topic.RowVersion));
    }

    public static List<TopicRule> ToRules(this IEnumerable<RuleInput> inputs) =>
        [.. inputs.Select(r => TopicRule.Create(r.Type, r.Value, r.IsExclusion))];
}
