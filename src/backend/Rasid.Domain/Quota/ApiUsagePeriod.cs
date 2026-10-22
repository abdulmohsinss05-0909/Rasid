using System.Globalization;
using Rasid.Domain.Common;

namespace Rasid.Domain.Quota;

/// <summary>
/// X API consumption for one calendar month (UTC), plus the latest rate-limit window reported by X.
/// Persisted so that every worker replica shares the same budget view.
/// </summary>
public sealed class ApiUsagePeriod : Entity<string>
{
    private ApiUsagePeriod()
    {
    }

    /// <summary>Posts read this month — the unit X uses for read caps.</summary>
    public long PostsRead { get; private set; }

    public long Requests { get; private set; }

    public int? RateLimitRemaining { get; private set; }

    public DateTimeOffset? RateLimitResetUtc { get; private set; }

    public DateTimeOffset UpdatedUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static string PeriodKey(DateTimeOffset nowUtc) =>
        nowUtc.ToUniversalTime().ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static ApiUsagePeriod Start(DateTimeOffset nowUtc) => new() { Id = PeriodKey(nowUtc), UpdatedUtc = nowUtc };

    public void RecordUsage(int postsRead, int requests, RateLimitWindow? window, DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(postsRead);
        ArgumentOutOfRangeException.ThrowIfNegative(requests);

        PostsRead += postsRead;
        Requests += requests;
        ApplyWindow(window);
        UpdatedUtc = nowUtc;
    }

    public void RecordRateLimited(DateTimeOffset resetUtc, DateTimeOffset nowUtc)
    {
        RateLimitRemaining = 0;
        RateLimitResetUtc = resetUtc;
        UpdatedUtc = nowUtc;
    }

    private void ApplyWindow(RateLimitWindow? window)
    {
        if (window is null)
        {
            return;
        }

        RateLimitRemaining = window.Remaining;
        RateLimitResetUtc = window.ResetUtc;
    }
}
