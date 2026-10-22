using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rasid.Domain.Engagement;
using Rasid.Domain.Posts;
using Rasid.Domain.Refresh;
using Rasid.Domain.Topics;

namespace Rasid.Infrastructure.Persistence.Configurations;

internal sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("Topics");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(Topic.NameMaxLength).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
        builder.Property(t => t.Description).HasMaxLength(Topic.DescriptionMaxLength);
        builder.Property(t => t.GeneratedQuery).HasMaxLength(4096).IsRequired();
        builder.Property(t => t.LastSeenPostId).HasMaxLength(32).IsUnicode(false);
        builder.Property(t => t.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(t => t.RowVersion).IsRowVersion();
        builder.HasIndex(t => t.IsActive);

        builder.HasMany(t => t.Rules)
            .WithOne()
            .HasForeignKey(r => r.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Rules).HasField("_rules").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany<TopicPost>()
            .WithOne()
            .HasForeignKey(tp => tp.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<TopicMetricsHourly>()
            .WithOne()
            .HasForeignKey(h => h.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<RefreshLog>()
            .WithOne()
            .HasForeignKey(l => l.TopicId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class TopicRuleConfiguration : IEntityTypeConfiguration<TopicRule>
{
    public void Configure(EntityTypeBuilder<TopicRule> builder)
    {
        builder.ToTable("TopicRules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Value).HasMaxLength(TopicRuleValue.KeywordMaxLength).IsRequired();
        builder.Ignore(r => r.DisplayValue);
    }
}
