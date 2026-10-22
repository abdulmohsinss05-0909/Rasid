using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;
using Rasid.Domain.Topics;

namespace Rasid.Application.Topics;

/// <param name="Version">The <see cref="TopicDto.Version"/> the client edited (optimistic concurrency).</param>
public sealed record UpdateTopicCommand(
    Guid Id,
    string Name,
    string? Description,
    TopicPriority Priority,
    IReadOnlyList<RuleInput> Rules,
    string Version) : ICommand<TopicDto>;

internal sealed class UpdateTopicValidator : AbstractValidator<UpdateTopicCommand>
{
    public UpdateTopicValidator(IOptions<MonitoringOptions> options)
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Version).NotEmpty();
        this.ApplyDetailsRules(c => c.Name, c => c.Description, c => c.Priority);
        this.ApplyRulesRules(c => c.Rules, options.Value);
    }
}

internal sealed class UpdateTopicHandler(
    IRasidDbContext db,
    IAuditTrail auditTrail,
    IOptions<MonitoringOptions> options,
    TimeProvider clock) : IRequestHandler<UpdateTopicCommand, TopicDto>
{
    public async Task<Result<TopicDto>> HandleAsync(UpdateTopicCommand request, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.Include(t => t.Rules).FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (topic is null)
        {
            return TopicErrors.NotFound(request.Id);
        }

        if (!string.Equals(Convert.ToBase64String(topic.RowVersion), request.Version, StringComparison.Ordinal))
        {
            return TopicErrors.ConcurrencyConflict();
        }

        var name = request.Name.Trim();
        if (await db.Topics.AnyAsync(t => t.Id != request.Id && t.Name == name, cancellationToken))
        {
            return TopicErrors.DuplicateName(name);
        }

        var now = clock.GetUtcNow();
        topic.Update(name, request.Description, request.Priority, request.Rules.ToRules(), now);
        auditTrail.Record(AuditActions.TopicUpdated, nameof(Topic), topic.Id.ToString(), topic.GeneratedQuery);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TopicErrors.ConcurrencyConflict();
        }

        var postCount = await db.TopicPosts.LongCountAsync(tp => tp.TopicId == topic.Id, cancellationToken);
        return topic.ToDto(postCount, options.Value, now);
    }
}
