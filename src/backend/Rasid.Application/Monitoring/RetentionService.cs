using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Options;
using Rasid.Domain.Posts;
using Rasid.Domain.Refresh;
using Rasid.Domain.Topics;

namespace Rasid.Application.Monitoring;

/// <summary>
/// Enforces the retention policy: purges post content and author details after the retention period,
/// trims metric history, old aggregates and operational logs. Aggregates contain no personal data and
/// are kept longer so trends remain available.
/// </summary>
public sealed partial class RetentionService(
    IRasidDbContext db,
    IAuditTrail auditTrail,
    IOptions<MonitoringOptions> options,
    TimeProvider clock,
    ILogger<RetentionService> logger)
{
    private static readonly TimeSpan RefreshLogRetention = TimeSpan.FromDays(90);

    public async Task<RetentionReport> PurgeAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();

        var posts = await db.Posts
            .Where(p => p.ContentExpiresUtc <= now)
            .ExecuteDeleteAsync(cancellationToken);

        var authors = await db.Authors
            .Where(a => !db.Posts.Any(p => p.AuthorId == a.Id))
            .ExecuteDeleteAsync(cancellationToken);

        var snapshotCutoff = now.AddDays(-settings.SnapshotRetentionDays);
        var snapshots = await db.MetricSnapshots
            .Where(s => s.CapturedUtc < snapshotCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var aggregateCutoff = now.AddDays(-settings.AggregateRetentionDays);
        var aggregates = await db.TopicMetricsHourly
            .Where(h => h.HourUtc < aggregateCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var logCutoff = now - RefreshLogRetention;
        var logs = await db.RefreshLogs
            .Where(l => l.StartedUtc < logCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var report = new RetentionReport(posts, authors, snapshots, aggregates, logs);

        var refreshLog = RefreshLog.Start(null, RefreshJobType.Retention, null, now);
        refreshLog.Complete(RefreshStatus.Success, clock.GetUtcNow(), posts, 0, message: report.ToString());
        db.RefreshLogs.Add(refreshLog);

        if (posts + authors > 0)
        {
            auditTrail.Record(AuditActions.ContentPurged, nameof(Post), entityId: null, details: report.ToString());
        }

        await db.SaveChangesAsync(cancellationToken);
        LogPurged(report.ToString());
        return report;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Retention run completed: {Report}")]
    private partial void LogPurged(string report);
}

public sealed record RetentionReport(int Posts, int Authors, int Snapshots, int Aggregates, int RefreshLogs)
{
    public override string ToString() =>
        $"Purged {Posts} post(s), {Authors} author(s), {Snapshots} snapshot(s), {Aggregates} aggregate row(s), {RefreshLogs} log row(s).";
}
