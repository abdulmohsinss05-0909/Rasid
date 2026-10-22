using System.Diagnostics;
using Rasid.Application.Common.Abstractions;
using Rasid.Infrastructure.Identity;

namespace Rasid.Api.Common;

/// <summary>The authenticated caller, read from the validated JWT.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private const string Anonymous = "anonymous";

    public string Id => accessor.HttpContext?.User.FindFirst(TokenService.SubjectClaim)?.Value ?? Anonymous;

    public string Name => accessor.HttpContext?.User.FindFirst(TokenService.NameClaim)?.Value ?? Anonymous;

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string? CorrelationId => Activity.Current?.TraceId.ToString() ?? accessor.HttpContext?.TraceIdentifier;
}
