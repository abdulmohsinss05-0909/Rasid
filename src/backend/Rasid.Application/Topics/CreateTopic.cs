using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;
using Rasid.Domain.Topics;

namespace Rasid.Application.Topics;

public sealed record CreateTopicCommand(
    string Name,
    string? Description,
    TopicPriority Priority,
    IReadOnlyList<RuleInput> Rules) : ICommand<TopicDto>;

internal sealed class CreateTopicValidator : AbstractValidator<CreateTopicCommand>
{
    public CreateTopicValidator(IOptions<MonitoringOptions> options)
    {
        this.ApplyDetailsRules(c => c.Name, c => c.Description, c => c.Priority);
        this.ApplyRulesRules(c => c.Rules, options.Value);
    }
}

internal sealed partial class CreateTopicHandler(
    IRasidDbContext db,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    IJobScheduler jobScheduler,
    IOptions<MonitoringOptions> options,
    TimeProvider clock,
    ILogger<CreateTopicHandler> logger) : IRequestHandler<CreateTopicCommand, TopicDto>
{
    public async Task<Result<TopicDto>> HandleAsync(CreateTopicCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await db.Topics.AnyAsync(t => t.Name == name, cancellationToken))
        {
            return TopicErrors.DuplicateName(name);
        }

        var now = clock.GetUtcNow();
        var topic = Topic.Create(name, request.Description, request.Priority, request.Rules.ToRules(), currentUser.Name, now);

        db.Topics.Add(topic);
        auditTrail.Record(AuditActions.TopicCreated, nameof(Topic), topic.Id.ToString(), topic.GeneratedQuery);
        await db.SaveChangesAsync(cancellationToken);

        QueueFirstCollection(topic.Id);
        return topic.ToDto(postCount: 0, options.Value, now);
    }

    private void QueueFirstCollection(Guid topicId)
    {
        try
        {
            jobScheduler.EnqueueTopicDiscovery(topicId);
        }
#pragma warning disable CA1031 // The topic is saved; the scheduler will pick it up on its next cycle if queuing fails.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogQueueFailed(ex, topicId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not queue first collection for topic {TopicId}; the scheduler will pick it up")]
    private partial void LogQueueFailed(Exception exception, Guid topicId);
}
