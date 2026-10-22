using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Options;
using Rasid.Application.Connectors;
using Rasid.Infrastructure.Connectors.Sample;
using Rasid.Infrastructure.Connectors.X;

namespace Rasid.Infrastructure.Connectors;

/// <summary>
/// Configuration-driven connector selection:
/// X credentials present → X API v2; otherwise the Sample connector, but only where sample data is
/// allowed (never in Production); otherwise no source, which surfaces as "not configured".
/// </summary>
internal sealed class PostSourceResolver(
    IServiceProvider services,
    IOptionsMonitor<XApiOptions> xOptions,
    IOptionsMonitor<MonitoringOptions> monitoringOptions) : IPostSourceResolver
{
    public IPostSource? Resolve()
    {
        if (xOptions.CurrentValue.IsConfigured)
        {
            return services.GetRequiredService<XPostSource>();
        }

        return monitoringOptions.CurrentValue.AllowSampleData
            ? services.GetRequiredService<SamplePostSource>()
            : null;
    }
}
