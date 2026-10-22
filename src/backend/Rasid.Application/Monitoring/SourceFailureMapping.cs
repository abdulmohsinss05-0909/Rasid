using Rasid.Application.Connectors;
using Rasid.Domain.Topics;

namespace Rasid.Application.Monitoring;

internal static class SourceFailureMapping
{
    public static RefreshStatus ToRefreshStatus(this SourceFailure failure) => failure.Kind switch
    {
        SourceFailureKind.Unauthorized => RefreshStatus.AuthFailed,
        SourceFailureKind.RateLimited => RefreshStatus.RateLimited,
        _ => RefreshStatus.ConnectionFailed,
    };
}
