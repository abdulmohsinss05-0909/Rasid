using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rasid.Domain.Auditing;
using Rasid.Domain.Engagement;
using Rasid.Domain.Quota;
using Rasid.Domain.Refresh;

namespace Rasid.Infrastructure.Persistence.Configurations;

internal sealed class TopicMetricsHourlyConfiguration : IEntityTypeConfiguration<TopicMetricsHourly>
{
    public void Configure(EntityTypeBuilder<TopicMetricsHourly> builder)
    {
        builder.ToTable("TopicMetricsHourly");
        builder.HasKey(h => new { h.TopicId, h.HourUtc });
        builder.HasIndex(h => h.HourUtc);
    }
}

internal sealed class RefreshLogConfiguration : IEntityTypeConfiguration<RefreshLog>
{
    public void Configure(EntityTypeBuilder<RefreshLog> builder)
    {
        builder.ToTable("RefreshLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).UseIdentityColumn();
        builder.Property(l => l.Message).HasMaxLength(RefreshLog.MessageMaxLength);
        builder.HasIndex(l => new { l.TopicId, l.JobType, l.StartedUtc });
        builder.HasIndex(l => l.StartedUtc);
    }
}

internal sealed class ApiUsagePeriodConfiguration : IEntityTypeConfiguration<ApiUsagePeriod>
{
    public void Configure(EntityTypeBuilder<ApiUsagePeriod> builder)
    {
        builder.ToTable("ApiUsagePeriods");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasMaxLength(7).IsUnicode(false).ValueGeneratedNever();
        builder.Property(p => p.RowVersion).IsRowVersion();
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityColumn();
        builder.Property(a => a.UserId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(64);
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.Property(a => a.CorrelationId).HasMaxLength(64);
        builder.HasIndex(a => a.OccurredUtc);
    }
}
