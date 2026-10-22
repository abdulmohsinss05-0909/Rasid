using FluentValidation;
using Rasid.Application.Common.Options;
using Rasid.Domain.Topics;

namespace Rasid.Application.Topics;

/// <summary>Validation shared by every command that accepts topic details or rules.</summary>
internal sealed class RuleInputValidator : AbstractValidator<RuleInput>
{
    public RuleInputValidator()
    {
        RuleFor(r => r.Type).IsInEnum();
        RuleFor(r => r.Value).Custom((value, context) =>
        {
            if (!TopicRuleValue.TryNormalize(context.InstanceToValidate.Type, value, out _, out var error))
            {
                context.AddFailure(error);
            }
        });
    }
}

internal static class TopicValidationRules
{
    public static void ApplyDetailsRules<T>(
        this AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, string>> name,
        System.Linq.Expressions.Expression<Func<T, string?>> description,
        System.Linq.Expressions.Expression<Func<T, TopicPriority>> priority)
    {
        validator.RuleFor(name).NotEmpty().MaximumLength(Topic.NameMaxLength);
        validator.RuleFor(description).MaximumLength(Topic.DescriptionMaxLength);
        validator.RuleFor(priority).IsInEnum();
    }

    public static void ApplyRulesRules<T>(
        this AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, IReadOnlyList<RuleInput>>> rules,
        MonitoringOptions options)
    {
        var getRules = rules.Compile();

        validator.RuleFor(rules)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Add at least one keyword, hashtag or account.")
            .Must(r => r.Count <= Topic.MaxRules).WithMessage($"A topic can have at most {Topic.MaxRules} rules.")
            .Must(r => r.Any(x => !x.IsExclusion)).WithMessage("At least one rule must be an inclusion (not an exclusion).");

        // Expression trees are not covariant: re-type the same lambda for RuleForEach.
        var eachRule = System.Linq.Expressions.Expression.Lambda<Func<T, IEnumerable<RuleInput>>>(rules.Body, rules.Parameters);
        validator.RuleForEach(eachRule).SetValidator(new RuleInputValidator());

        // Only checked once every rule is individually valid, so the query can actually be built.
        validator.RuleFor(rules)
            .Must(r => XQueryBuilder.Build(r.ToRules()).Length <= options.MaxQueryLength)
            .When(x => AllRulesValid(getRules(x)))
            .WithMessage($"The generated X query exceeds the plan limit of {options.MaxQueryLength} characters. Split the topic or remove rules.");
    }

    private static bool AllRulesValid(IReadOnlyList<RuleInput>? rules) =>
        rules is { Count: > 0 and <= Topic.MaxRules }
        && rules.Any(r => !r.IsExclusion)
        && rules.All(r => Enum.IsDefined(r.Type) && TopicRuleValue.TryNormalize(r.Type, r.Value, out _, out _));
}
