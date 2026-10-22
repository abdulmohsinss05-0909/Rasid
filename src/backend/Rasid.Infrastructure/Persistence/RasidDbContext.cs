using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rasid.Application.Common.Abstractions;
using Rasid.Domain.Auditing;
using Rasid.Domain.Engagement;
using Rasid.Domain.Posts;
using Rasid.Domain.Quota;
using Rasid.Domain.Refresh;
using Rasid.Domain.Topics;
using Rasid.Infrastructure.Identity;

namespace Rasid.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context. Application tables live in the <c>rasid</c> schema, identity tables in
/// <c>auth</c> and Hangfire in <c>hangfire</c> — so database permissions can be granted per schema.
/// </summary>
public sealed class RasidDbContext(DbContextOptions<RasidDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole, string>(options), IRasidDbContext
{
    public const string AppSchema = "rasid";
    public const string AuthSchema = "auth";

    public DbSet<Topic> Topics => Set<Topic>();

    public DbSet<TopicRule> TopicRules => Set<TopicRule>();

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<Author> Authors => Set<Author>();

    public DbSet<TopicPost> TopicPosts => Set<TopicPost>();

    public DbSet<MetricSnapshot> MetricSnapshots => Set<MetricSnapshot>();

    public DbSet<TopicMetricsHourly> TopicMetricsHourly => Set<TopicMetricsHourly>();

    public DbSet<RefreshLog> RefreshLogs => Set<RefreshLog>();

    public DbSet<ApiUsagePeriod> ApiUsagePeriods => Set<ApiUsagePeriod>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(AppSchema);
        builder.ApplyConfigurationsFromAssembly(typeof(RasidDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Enums are stored as readable strings: safer for reporting and for reordering enum members.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
