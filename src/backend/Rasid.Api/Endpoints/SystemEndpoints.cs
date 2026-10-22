using Rasid.Api.Common;
using Rasid.Application.Auth;
using Rasid.Application.Common.Messaging;
using Rasid.Application.SystemStatus;

namespace Rasid.Api.Endpoints;

internal sealed class SystemEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder api)
    {
        api.MapGroup("/system")
            .WithTags("System")
            .RequireAuthorization(Policies.CanAdminister)
            .MapGet("/status", async (ISender sender, CancellationToken ct) =>
                (await sender.SendAsync(new GetSystemStatusQuery(), ct)).ToHttpResult())
            .WithSummary("Active data source, X API quota usage and recent collection runs.");
    }
}
