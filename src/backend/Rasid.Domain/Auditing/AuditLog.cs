using Rasid.Domain.Common;

namespace Rasid.Domain.Auditing;

/// <summary>Append-only record of who did what, for security review and compliance.</summary>
public sealed class AuditLog : Entity<long>
{
    private AuditLog()
    {
    }

    public string UserId { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public string? Details { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset OccurredUtc { get; private set; }

    public static AuditLog Record(
        string userId,
        string action,
        string entityType,
        string? entityId,
        DateTimeOffset nowUtc,
        string? correlationId = null,
        string? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        return new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            CorrelationId = correlationId,
            OccurredUtc = nowUtc,
        };
    }
}
