using Rasid.Api.Common;
using Rasid.Application.Auth;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Paging;
using Rasid.Application.Dashboard;
using Rasid.Application.Insights;
using Rasid.Domain.Engagement;

namespace Rasid.Api.Endpoints;

internal sealed class DashboardEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder api)
    {
        var topics = api.MapGroup("/topics/{id:guid}").WithTags("Dashboard").RequireAuthorization(Policies.CanView);

        topics.MapGet("/dashboard", async (
                Guid id,
                DateTimeOffset? fromUtc,
                DateTimeOffset? toUtc,
                string? keyword,
                EngagementType? engagementType,
                EngagementLevel? level,
                int? utcOffsetMinutes,
                ISender sender,
                CancellationToken ct) =>
                (await sender.SendAsync(
                    new GetTopicDashboardQuery(
                        id, fromUtc, toUtc, keyword, engagementType ?? EngagementType.All, level, utcOffsetMinutes ?? 0),
                    ct)).ToHttpResult())
            .WithSummary("KPIs, time series, breakdowns, top posts and data freshness for a topic.");

        topics.MapGet("/posts", async (
                Guid id,
                DateTimeOffset? fromUtc,
                DateTimeOffset? toUtc,
                string? keyword,
                EngagementType? engagementType,
                EngagementLevel? level,
                PostSort? sort,
                int? page,
                int? pageSize,
                ISender sender,
                CancellationToken ct) =>
                (await sender.SendAsync(
                    new GetTopicPostsQuery(
                        id,
                        fromUtc,
                        toUtc,
                        keyword,
                        engagementType ?? EngagementType.All,
                        level,
                        sort ?? PostSort.Recent,
                        page ?? 1,
                        pageSize ?? Paging.DefaultPageSize),
                    ct)).ToHttpResult())
            .WithSummary("Filtered, sorted and paged posts of a topic.");

        topics.MapGet("/insights", async (
                Guid id,
                DateTimeOffset? fromUtc,
                DateTimeOffset? toUtc,
                string? keyword,
                EngagementLevel? level,
                int? utcOffsetMinutes,
                ISender sender,
                CancellationToken ct) =>
                (await sender.SendAsync(
                    new GetTopicInsightsQuery(id, fromUtc, toUtc, keyword, EngagementType.All, level, utcOffsetMinutes ?? 0),
                    ct)).ToHttpResult())
            .WithSummary("Top hashtags, top accounts, activity heatmap, rule performance, languages and key highlights.");
    }
}
