using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Rasid.Application.Connectors;
using Rasid.Domain.Common;

namespace Rasid.Infrastructure.Services;

/// <summary>
/// Readiness: a data source must be configured. In Production a missing source is Unhealthy
/// (fails fast instead of silently showing nothing); elsewhere it is Degraded.
/// </summary>
internal sealed class DataSourceHealthCheck(IPostSourceResolver resolver, IHostEnvironment environment) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var source = resolver.Resolve()?.Source;
        var data = new Dictionary<string, object> { ["source"] = source?.ToString() ?? "none" };

        var result = source switch
        {
            DataSource.XApi => HealthCheckResult.Healthy("X API v2 connector configured.", data),
            DataSource.Sample => HealthCheckResult.Degraded("Running on SAMPLE DATA (no X credentials configured).", data: data),
            _ when environment.IsProduction() => HealthCheckResult.Unhealthy("No data source configured.", data: data),
            _ => HealthCheckResult.Degraded("No data source configured.", data: data),
        };

        return Task.FromResult(result);
    }
}
