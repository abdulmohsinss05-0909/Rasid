namespace Rasid.Domain.Quota;

/// <summary>Rate-limit window as reported by X in <c>x-rate-limit-*</c> response headers.</summary>
public sealed record RateLimitWindow(int Limit, int Remaining, DateTimeOffset ResetUtc);
