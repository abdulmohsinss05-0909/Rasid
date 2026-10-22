using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;

namespace Rasid.Application.Topics;

public sealed record GetTopicsQuery : IQuery<IReadOnlyList<TopicDto>>;

public sealed record GetTopicQuery(Guid Id) : IQuery<TopicDto>;

internal sealed class GetTopicValidator : AbstractValidator<GetTopicQuery>
{
    public GetTopicValidator() => RuleFor(q => q.Id).NotEmpty();
}

internal sealed class GetTopicsHandler(IRasidDbContext db, IOptions<MonitoringOptions> options, TimeProvider clock)
    : IRequestHandler<GetTopicsQuery, IReadOnlyList<TopicDto>>
{
    public async Task<Result<IReadOnlyList<TopicDto>>> HandleAsync(GetTopicsQuery request, CancellationToken cancellationToken)
    {
        var topics = await db.Topics
            .AsNoTracking()
            .Include(t => t.Rules)
            .OrderByDescending(t => t.IsActive)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var counts = await db.TopicPosts
            .GroupBy(tp => tp.TopicId)
            .Select(g => new { TopicId = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(x => x.TopicId, x => x.Count, cancellationToken);

        var now = clock.GetUtcNow();
        return topics
            .Select(t => t.ToDto(counts.GetValueOrDefault(t.Id), options.Value, now))
            .ToList();
    }
}

internal sealed class GetTopicHandler(IRasidDbContext db, IOptions<MonitoringOptions> options, TimeProvider clock)
    : IRequestHandler<GetTopicQuery, TopicDto>
{
    public async Task<Result<TopicDto>> HandleAsync(GetTopicQuery request, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.AsNoTracking().Include(t => t.Rules)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (topic is null)
        {
            return TopicErrors.NotFound(request.Id);
        }

        var postCount = await db.TopicPosts.LongCountAsync(tp => tp.TopicId == topic.Id, cancellationToken);
        return topic.ToDto(postCount, options.Value, clock.GetUtcNow());
    }
}
