using Rasid.Application.Common.Abstractions;
using Rasid.Domain.Auditing;

namespace Rasid.Infrastructure.Services;

internal sealed class AuditTrail(IRasidDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAuditTrail
{
    public void Record(string action, string entityType, string? entityId, string? details = null) =>
        db.AuditLogs.Add(AuditLog.Record(
            currentUser.Id,
            action,
            entityType,
            entityId,
            clock.GetUtcNow(),
            currentUser.CorrelationId,
            details));
}

/// <summary>Identity used by background jobs.</summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public string Id => "system";

    public string Name => "system";

    public bool IsAuthenticated => true;

    public string? CorrelationId => System.Diagnostics.Activity.Current?.TraceId.ToString();
}
