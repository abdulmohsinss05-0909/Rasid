using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;
using Rasid.Domain.Topics;

namespace Rasid.Application.Topics;

/// <summary>"Refresh now": queues an immediate discovery run, with a cooldown to protect the API quota.</summary>
public sealed record RequestTopicRefreshCommand(Guid Id) : ICommand<RefreshRequestedDto>;

internal sealed class RequestTopicRefreshValidator : AbstractValidator<RequestTopicRefreshCommand>
{
    public RequestTopicRefreshValidator() => RuleFor(c => c.Id).NotEmpty();
}

internal sealed class RequestTopicRefreshHandler(
    IRasidDbContext db,
    IJobScheduler jobScheduler,
    IAuditTrail auditTrail,
    IOptions<MonitoringOptions> options,
    TimeProvider clock) : IRequestHandler<RequestTopicRefreshCommand, RefreshRequestedDto>
{
    public async Task<Result<RefreshRequestedDto>> HandleAsync(RequestTopicRefreshCommand request, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (topic is null)
        {
            return TopicErrors.NotFound(request.Id);
        }

        if (!topic.IsActive)
        {
            return TopicErrors.Paused();
        }

        var now = clock.GetUtcNow();
        var cooldown = TimeSpan.FromSeconds(options.Value.ManualRefreshCooldownSeconds);
        var wait = topic.ManualRefreshWait(now, cooldown);
        if (wait > TimeSpan.Zero)
        {
            return TopicErrors.RefreshCooldown((int)Math.Ceiling(wait.TotalSeconds));
        }

        // Recorded before queuing; the row version makes concurrent double-clicks fail instead of both passing.
        topic.RequestManualRefresh(now, cooldown);
        auditTrail.Record(AuditActions.TopicRefreshRequested, nameof(Topic), topic.Id.ToString());

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TopicErrors.RefreshCooldown((int)cooldown.TotalSeconds);
        }

        var jobId = jobScheduler.EnqueueTopicDiscovery(topic.Id);
        return new RefreshRequestedDto(topic.Id, jobId, "Refresh queued. New data will appear shortly.");
    }
}
