using Rasid.Api.Common;
using Rasid.Application.Alerts;
using Rasid.Application.Auth;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Overview;

namespace Rasid.Api.Endpoints;

internal sealed class OverviewEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/overview", async (int? utcOffsetMinutes, ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new GetOverviewQuery(utcOffsetMinutes ?? 0), ct)).ToHttpResult())
            .WithTags("Overview")
            .RequireAuthorization(Policies.CanView)
            .WithSummary("All topics at a glance: last 7 days versus the previous 7, with daily sparklines.");

        api.MapGet("/alerts", async (ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new GetAlertsQuery(), ct)).ToHttpResult())
            .WithTags("Overview")
            .RequireAuthorization(Policies.CanView)
            .WithSummary("Current alerts: activity spikes, collection problems, stale data and API budget pressure.");
    }
}
