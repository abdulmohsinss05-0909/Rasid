namespace Rasid.Domain.Common;

/// <summary>Base type for all entities: identity is what makes two instances the same.</summary>
public abstract class Entity<TId>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;
}
