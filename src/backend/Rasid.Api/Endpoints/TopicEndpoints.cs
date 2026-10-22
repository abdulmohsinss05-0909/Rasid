using Rasid.Api.Common;
using Rasid.Application.Auth;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Topics;
using Rasid.Domain.Topics;

namespace Rasid.Api.Endpoints;

internal sealed class TopicEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder api)
    {
        var topics = api.MapGroup("/topics").WithTags("Topics").RequireAuthorization(Policies.CanView);

        topics.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new GetTopicsQuery(), ct)).ToHttpResult())
            .WithSummary("List monitoring topics with their collection status.");

        topics.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new GetTopicQuery(id), ct)).ToHttpResult())
            .WithSummary("Get one topic.");

        topics.MapPost("/", async (TopicRequest body, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new CreateTopicCommand(body.Name, body.Description, body.Priority, body.Rules ?? []), ct))
                    .ToHttpResult(topic => TypedResults.Created($"{EndpointGroupExtensions.ApiPrefix}/topics/{topic.Id}", topic)))
            .RequireAuthorization(Policies.CanManageTopics)
            .WithSummary("Create a topic. Its first collection is queued immediately.");

        topics.MapPut("/{id:guid}", async (Guid id, UpdateTopicRequest body, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(
                    new UpdateTopicCommand(id, body.Name, body.Description, body.Priority, body.Rules ?? [], body.Version), ct))
                    .ToHttpResult())
            .RequireAuthorization(Policies.CanManageTopics)
            .WithSummary("Update a topic (optimistic concurrency via 'version').");

        topics.MapPost("/{id:guid}/pause", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new SetTopicActiveCommand(id, IsActive: false), ct)).ToHttpResult())
            .RequireAuthorization(Policies.CanManageTopics)
            .WithSummary("Pause data collection.");

        topics.MapPost("/{id:guid}/resume", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new SetTopicActiveCommand(id, IsActive: true), ct)).ToHttpResult())
            .RequireAuthorization(Policies.CanManageTopics)
            .WithSummary("Resume data collection.");

        topics.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new DeleteTopicCommand(id), ct)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequireAuthorization(Policies.CanManageTopics)
            .WithSummary("Delete a topic and data no other topic uses.");

        topics.MapPost("/{id:guid}/refresh", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new RequestTopicRefreshCommand(id), ct)).ToHttpResult(r => TypedResults.Accepted((string?)null, r)))
            .RequireAuthorization(Policies.CanManageTopics)
            .WithSummary("Queue an immediate collection run (rate-limited per topic).");

        topics.MapPost("/preview-query", async (PreviewRequest body, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new PreviewTopicQuery(body.Rules ?? []), ct)).ToHttpResult())
            .RequireAuthorization(Policies.CanManageTopics)
            .WithSummary("Build the X query for a set of rules without saving.");
    }

    internal sealed record TopicRequest(string Name, string? Description, TopicPriority Priority, IReadOnlyList<RuleInput>? Rules);

    internal sealed record UpdateTopicRequest(
        string Name, string? Description, TopicPriority Priority, IReadOnlyList<RuleInput>? Rules, string Version);

    internal sealed record PreviewRequest(IReadOnlyList<RuleInput>? Rules);
}
