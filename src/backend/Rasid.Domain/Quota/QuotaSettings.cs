using Rasid.Domain.Common;

namespace Rasid.Domain.Quota;

/// <summary>
/// Budget policy for the X API plan in use.
/// <see cref="MonthlyPostReadBudget"/> of 0 means "no monthly cap" (only rate-limit windows apply).
/// </summary>
public sealed record QuotaSettings
{
    public QuotaSettings(
        long monthlyPostReadBudget,
        decimal throttleAtPercent = 80m,
        decimal criticalAtPercent = 95m,
        int maxPagesPerRun = 3)
    {
        if (monthlyPostReadBudget < 0)
        {
            throw new DomainException("Monthly budget cannot be negative.");
        }

        if (throttleAtPercent <= 0 || criticalAtPercent <= throttleAtPercent || criticalAtPercent > 100)
        {
            throw new DomainException("Quota thresholds must satisfy 0 < throttle < critical <= 100.");
        }

        if (maxPagesPerRun < 1)
        {
            throw new DomainException("At least one page per run is required.");
        }

        MonthlyPostReadBudget = monthlyPostReadBudget;
        ThrottleAtPercent = throttleAtPercent;
        CriticalAtPercent = criticalAtPercent;
        MaxPagesPerRun = maxPagesPerRun;
    }

    public long MonthlyPostReadBudget { get; }

    public decimal ThrottleAtPercent { get; }

    public decimal CriticalAtPercent { get; }

    public int MaxPagesPerRun { get; }
}
