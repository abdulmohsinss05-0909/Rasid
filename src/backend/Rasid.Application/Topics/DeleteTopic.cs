using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Results;
using Rasid.Domain.Topics;

namespace Rasid.Application.Topics;

/// <summary>Deletes a topic, its aggregates and any posts no other topic references.</summary>
public sealed record DeleteTopicCommand(Guid Id) : ICommand<Unit>;

internal sealed class DeleteTopicValidator : AbstractValidator<DeleteTopicCommand>
{
    public DeleteTopicValidator() => RuleFor(c => c.Id).NotEmpty();
}

internal sealed class DeleteTopicHandler(IRasidDbContext db, IAuditTrail auditTrail) : IRequestHandler<DeleteTopicCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeleteTopicCommand request, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (topic is null)
        {
            return TopicErrors.NotFound(request.Id);
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Rules, post links and aggregates cascade with the topic.
            db.Topics.Remove(topic);
            auditTrail.Record(AuditActions.TopicDeleted, nameof(Topic), topic.Id.ToString(), topic.Name);
            await db.SaveChangesAsync(cancellationToken);

            // Data minimisation: posts that no remaining topic references are removed too.
            await db.Posts
                .Where(p => !db.TopicPosts.Any(tp => tp.PostId == p.Id))
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });

        return Unit.Value;
    }
}
