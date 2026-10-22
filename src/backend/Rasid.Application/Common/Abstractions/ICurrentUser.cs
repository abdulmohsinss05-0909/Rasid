namespace Rasid.Application.Common.Abstractions;

/// <summary>The identity on whose behalf the current operation runs (a user, or the system for jobs).</summary>
public interface ICurrentUser
{
    string Id { get; }

    string Name { get; }

    bool IsAuthenticated { get; }

    string? CorrelationId { get; }
}
