using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Paging;
using Rasid.Application.Common.Results;
using Rasid.Application.Topics;
using Rasid.Domain.Engagement;

namespace Rasid.Application.Dashboard;

public sealed record GetTopicPostsQuery(
    Guid TopicId,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Keyword = null,
    EngagementType EngagementType = EngagementType.All,
    EngagementLevel? Level = null,
    PostSort Sort = PostSort.Recent,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<PostDto>>, IPostFilterRequest;

internal sealed class GetTopicPostsValidator : AbstractValidator<GetTopicPostsQuery>
{
    public GetTopicPostsValidator()
    {
        this.ApplyFilterRules();
        RuleFor(q => q.Sort).IsInEnum();
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

internal sealed class GetTopicPostsHandler(IRasidDbContext db, IOptions<MonitoringOptions> options, TimeProvider clock)
    : IRequestHandler<GetTopicPostsQuery, PagedResult<PostDto>>
{
    public async Task<Result<PagedResult<PostDto>>> HandleAsync(GetTopicPostsQuery request, CancellationToken cancellationToken)
    {
        if (!await db.Topics.AnyAsync(t => t.Id == request.TopicId, cancellationToken))
        {
            return TopicErrors.NotFound(request.TopicId);
        }

        var thresholds = options.Value.Engagement.ToThresholds();
        var (from, to) = DashboardRange.Resolve(request.FromUtc, request.ToUtc, clock.GetUtcNow());

        var filtered = db.Posts.AsNoTracking()
            .ForTopic(db.TopicPosts, request.TopicId)
            .CreatedBetween(from, to)
            .ContainingKeyword(request.Keyword)
            .WithLevel(request.Level, thresholds);

        var total = await filtered.CountAsync(cancellationToken);
        var items = await filtered
            .SortBy(request.Sort, request.EngagementType)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToDtos()
            .ToListAsync(cancellationToken);

        return new PagedResult<PostDto>(
            [.. items.Select(p => p.WithEngagement(thresholds))],
            request.Page,
            request.PageSize,
            total);
    }
}
