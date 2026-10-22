using FluentValidation;
using Rasid.Domain.Engagement;

namespace Rasid.Application.Dashboard;

/// <summary>Filter fields common to the dashboard and the posts list.</summary>
public interface IPostFilterRequest
{
    Guid TopicId { get; }

    DateTimeOffset? FromUtc { get; }

    DateTimeOffset? ToUtc { get; }

    string? Keyword { get; }

    EngagementType EngagementType { get; }

    EngagementLevel? Level { get; }
}

internal static class DashboardFilterValidation
{
    public const int KeywordMaxLength = 100;

    public static void ApplyFilterRules<T>(this AbstractValidator<T> validator)
        where T : IPostFilterRequest
    {
        validator.RuleFor(q => q.TopicId).NotEmpty();
        validator.RuleFor(q => q.EngagementType).IsInEnum();
        validator.RuleFor(q => q.Level).IsInEnum().When(q => q.Level is not null);
        validator.RuleFor(q => q.Keyword).MaximumLength(KeywordMaxLength);

        validator.RuleFor(q => q)
            .Must(q => q.FromUtc is null || q.ToUtc is null || q.FromUtc <= q.ToUtc)
            .WithName("fromUtc")
            .WithMessage("'From' must be before 'To'.");

        validator.RuleFor(q => q)
            .Must(q => q.FromUtc is null || q.ToUtc is null || (q.ToUtc.Value - q.FromUtc.Value).TotalDays <= DashboardRange.MaxRangeDays)
            .WithName("toUtc")
            .WithMessage($"The period cannot exceed {DashboardRange.MaxRangeDays} days.");
    }
}
