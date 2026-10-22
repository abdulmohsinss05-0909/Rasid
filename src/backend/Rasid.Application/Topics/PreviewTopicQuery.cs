using FluentValidation;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;
using Rasid.Domain.Topics;

namespace Rasid.Application.Topics;

/// <summary>Builds the X query for a set of rules without saving anything — powers the live preview in the form.</summary>
public sealed record PreviewTopicQuery(IReadOnlyList<RuleInput> Rules) : IQuery<QueryPreviewDto>;

internal sealed class PreviewTopicQueryValidator : AbstractValidator<PreviewTopicQuery>
{
    public PreviewTopicQueryValidator()
    {
        RuleFor(q => q.Rules)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Add at least one keyword, hashtag or account.")
            .Must(r => r.Count <= Topic.MaxRules).WithMessage($"A topic can have at most {Topic.MaxRules} rules.")
            .Must(r => r.Any(x => !x.IsExclusion)).WithMessage("At least one rule must be an inclusion (not an exclusion).");
        RuleForEach(q => q.Rules).SetValidator(new RuleInputValidator());
    }
}

internal sealed class PreviewTopicQueryHandler(IOptions<MonitoringOptions> options)
    : IRequestHandler<PreviewTopicQuery, QueryPreviewDto>
{
    public Task<Result<QueryPreviewDto>> HandleAsync(PreviewTopicQuery request, CancellationToken cancellationToken)
    {
        var query = XQueryBuilder.Build(request.Rules.ToRules());
        var maxLength = options.Value.MaxQueryLength;

        Result<QueryPreviewDto> result = new QueryPreviewDto(query, query.Length, maxLength, query.Length <= maxLength);
        return Task.FromResult(result);
    }
}
