using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Rasid.Infrastructure.Configuration;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Adds <c>config/endpoints.{Environment}.json</c> right after the appsettings files, so environment
    /// variables and secret stores can still override any URL (e.g. inside Docker).
    /// </summary>
    public static IConfigurationBuilder AddEndpointsFile(this IConfigurationBuilder configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var source = new JsonConfigurationSource
        {
            Path = Path.Combine("config", $"endpoints.{environment.EnvironmentName}.json"),
            Optional = false,
            ReloadOnChange = false,
            // The files are copied next to the binaries at build/publish time.
            FileProvider = new PhysicalFileProvider(AppContext.BaseDirectory),
        };

        var lastAppSettings = -1;
        for (var i = 0; i < configuration.Sources.Count; i++)
        {
            if (configuration.Sources[i] is JsonConfigurationSource json
                && json.Path?.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) == true)
            {
                lastAppSettings = i;
            }
        }

        configuration.Sources.Insert(lastAppSettings + 1, source);
        return configuration;
    }
}
