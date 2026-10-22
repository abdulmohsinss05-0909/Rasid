using Rasid.Domain.Topics;

namespace Rasid.Application.Topics;

public sealed record RuleInput(RuleType Type, string Value, bool IsExclusion = false);

public sealed record RuleDto(Guid Id, RuleType Type, string Value, string DisplayValue, bool IsExclusion);

public sealed record TopicDto(
    Guid Id,
    string Name,
    string? Description,
    TopicPriority Priority,
    bool IsActive,
    IReadOnlyList<RuleDto> Rules,
    string GeneratedQuery,
    long PostCount,
    RefreshStatus LastRefreshStatus,
    DateTimeOffset? LastRefreshAttemptUtc,
    DateTimeOffset? LastSuccessfulRefreshUtc,
    bool IsStale,
    int RefreshIntervalMinutes,
    DateTimeOffset CreatedUtc,
    string CreatedBy,
    DateTimeOffset? UpdatedUtc,
    string Version);

public sealed record QueryPreviewDto(string Query, int Length, int MaxLength, bool IsWithinLimit);

public sealed record RefreshRequestedDto(Guid TopicId, string JobId, string Message);
