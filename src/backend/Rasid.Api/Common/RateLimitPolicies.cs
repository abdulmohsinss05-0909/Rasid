namespace Rasid.Api.Common;

internal static class RateLimitPolicies
{
    /// <summary>Strict limit for sign-in and token endpoints (anti brute-force).</summary>
    public const string Auth = "auth";
}
