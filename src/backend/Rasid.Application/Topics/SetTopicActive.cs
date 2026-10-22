using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;
using Rasid.Domain.Topics;

namespace Rasid.Application.Topics;

/// <summary>Pauses or resumes data collection for a topic.</summary>
public sealed record SetTopicActiveCommand(Guid Id, bool IsActive) : ICommand<TopicDto>;

internal sealed class SetTopicActiveValidator : AbstractValidator<SetTopicActiveCommand>
{
    public SetTopicActiveValidator() => RuleFor(c => c.Id).NotEmpty();
}

internal sealed class SetTopicActiveHandler(
    IRasidDbContext db,
    IAuditTrail auditTrail,
    IOptions<MonitoringOptions> options,
    TimeProvider clock) : IRequestHandler<SetTopicActiveCommand, TopicDto>
{
    public async Task<Result<TopicDto>> HandleAsync(SetTopicActiveCommand request, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.Include(t => t.Rules).FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (topic is null)
        {
            return TopicErrors.NotFound(request.Id);
        }

        var now = clock.GetUtcNow();
        if (request.IsActive)
        {
            topic.Resume(now);
            auditTrail.Record(AuditActions.TopicResumed, nameof(Topic), topic.Id.ToString());
        }
        else
        {
            topic.Pause(now);
            auditTrail.Record(AuditActions.TopicPaused, nameof(Topic), topic.Id.ToString());
        }

        await db.SaveChangesAsync(cancellationToken);

        var postCount = await db.TopicPosts.LongCountAsync(tp => tp.TopicId == topic.Id, cancellationToken);
        return topic.ToDto(postCount, options.Value, now);
    }
}
