using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rasid.Domain.Engagement;
using Rasid.Domain.Posts;

namespace Rasid.Infrastructure.Persistence.Configurations;

internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public const int IdMaxLength = 32;

    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasMaxLength(IdMaxLength).IsUnicode(false).ValueGeneratedNever();
        builder.Property(p => p.AuthorId).HasMaxLength(IdMaxLength).IsUnicode(false).IsRequired();
        builder.Property(p => p.Text).IsRequired();
        builder.Property(p => p.Language).HasMaxLength(16).IsUnicode(false);
        builder.Property(p => p.Url).HasMaxLength(512);
        builder.Property(p => p.MediaKind).HasMaxLength(PostMedia.KindMaxLength);
        builder.Property(p => p.MediaPreviewUrl).HasMaxLength(PostMedia.UrlMaxLength);
        builder.Property(p => p.MediaAltText).HasMaxLength(PostMedia.AltTextMaxLength);
        builder.Ignore(p => p.Media);

        builder.ComplexProperty(p => p.Metrics, metrics => metrics.MapMetricColumns());

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<MetricSnapshot>()
            .WithOne()
            .HasForeignKey(s => s.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<TopicPost>()
            .WithOne(tp => tp.Post)
            .HasForeignKey(tp => tp.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.CreatedAtUtc);
        builder.HasIndex(p => p.ContentExpiresUtc);
        // Supports the tiered metric-refresh selection.
        builder.HasIndex(p => new { p.Source, p.MetricsCapturedUtc });
    }
}

internal sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable("Authors");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasMaxLength(PostConfiguration.IdMaxLength).IsUnicode(false).ValueGeneratedNever();
        builder.Property(a => a.Username).HasMaxLength(50).IsRequired();
        builder.Property(a => a.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ProfileImageUrl).HasMaxLength(512);
    }
}

internal sealed class TopicPostConfiguration : IEntityTypeConfiguration<TopicPost>
{
    public void Configure(EntityTypeBuilder<TopicPost> builder)
    {
        builder.ToTable("TopicPosts");
        builder.HasKey(tp => new { tp.TopicId, tp.PostId });
        builder.Property(tp => tp.PostId).HasMaxLength(PostConfiguration.IdMaxLength).IsUnicode(false);
        builder.HasIndex(tp => tp.PostId);
    }
}

internal sealed class MetricSnapshotConfiguration : IEntityTypeConfiguration<MetricSnapshot>
{
    public void Configure(EntityTypeBuilder<MetricSnapshot> builder)
    {
        builder.ToTable("MetricSnapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).UseIdentityColumn();
        builder.Property(s => s.PostId).HasMaxLength(PostConfiguration.IdMaxLength).IsUnicode(false).IsRequired();
        builder.ComplexProperty(s => s.Metrics, metrics => metrics.MapMetricColumns());
        builder.HasIndex(s => new { s.PostId, s.CapturedUtc });
        builder.HasIndex(s => s.CapturedUtc);
    }
}

internal static class MetricColumns
{
    /// <summary>Same column names wherever <see cref="EngagementMetrics"/> is stored.</summary>
    public static void MapMetricColumns(this ComplexPropertyBuilder<EngagementMetrics> metrics)
    {
        metrics.Property(m => m.Likes).HasColumnName("Likes");
        metrics.Property(m => m.Reposts).HasColumnName("Reposts");
        metrics.Property(m => m.Replies).HasColumnName("Replies");
        metrics.Property(m => m.Quotes).HasColumnName("Quotes");
        metrics.Property(m => m.Views).HasColumnName("Views");
    }
}
