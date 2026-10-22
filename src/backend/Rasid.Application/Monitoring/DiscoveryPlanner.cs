using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Options;
using Rasid.Application.Connectors;
using Rasid.Domain.Topics;

namespace Rasid.Application.Monitoring;

/// <summary>
/// Selects the topics whose discovery run is due, applying the priority cadence and the
/// Quota Manager's throttling multiplier (lower priorities slow down as the budget is consumed).
/// </summary>
public sealed class DiscoveryPlanner(
    IRasidDbContext db,
    IPostSourceResolver sourceResolver,
    IQuotaManager quota,
    IOptions<MonitoringOptions> options,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<Guid>> FindDueTopicsAsync(CancellationToken cancellationToken)
    {
        var source = sourceResolver.Resolve();
        if (source is null)
        {
            return [];
        }

        var multipliers = new Dictionary<TopicPriority, int>();
        foreach (var priority in Enum.GetValues<TopicPriority>())
        {
            var decision = await quota.EvaluateAsync(source.Source, priority, cancellationToken);
            if (decision.IsAllowed)
            {
                multipliers[priority] = decision.IntervalMultiplier;
            }
        }

        var now = clock.GetUtcNow();
        var activeTopics = await db.Topics.AsNoTracking().Where(t => t.IsActive).ToListAsync(cancellationToken);

        return activeTopics
            .Where(t => multipliers.TryGetValue(t.Priority, out var multiplier)
                && t.IsDueForRefresh(now, options.Value.RefreshInterval(t.Priority) * multiplier))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.LastRefreshAttemptUtc ?? DateTimeOffset.MinValue)
            .Select(t => t.Id)
            .ToList();
    }
}
