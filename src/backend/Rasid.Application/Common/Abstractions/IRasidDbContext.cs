using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Rasid.Domain.Auditing;
using Rasid.Domain.Engagement;
using Rasid.Domain.Posts;
using Rasid.Domain.Quota;
using Rasid.Domain.Refresh;
using Rasid.Domain.Topics;

namespace Rasid.Application.Common.Abstractions;

/// <summary>Persistence port. Implemented by the EF Core context in Infrastructure.</summary>
public interface IRasidDbContext
{
    DbSet<Topic> Topics { get; }

    DbSet<TopicRule> TopicRules { get; }

    DbSet<Post> Posts { get; }

    DbSet<Author> Authors { get; }

    DbSet<TopicPost> TopicPosts { get; }

    DbSet<MetricSnapshot> MetricSnapshots { get; }

    DbSet<TopicMetricsHourly> TopicMetricsHourly { get; }

    DbSet<RefreshLog> RefreshLogs { get; }

    DbSet<ApiUsagePeriod> ApiUsagePeriods { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
