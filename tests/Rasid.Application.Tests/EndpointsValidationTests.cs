using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Rasid.Infrastructure.Configuration;

namespace Rasid.Application.Tests;

/// <summary>
/// Start-up validation of <c>endpoints.{Environment}.json</c>: HTTPS outside Development, with loopback HTTP
/// allowed only in UAT (the local Docker stack). Production and the X API never accept HTTP.
/// </summary>
public sealed class EndpointsValidationTests
{
    [Theory]
    [InlineData("Development", "http://localhost:4200")]
    [InlineData("UAT", "https://uat.rasid.example.com")]
    [InlineData("UAT", "http://localhost:8080")]
    [InlineData("UAT", "http://127.0.0.1:8080")]
    [InlineData("Production", "https://rasid.example.com")]
    public void Accepts_allowed_urls(string environment, string url)
    {
        var result = Validate(environment, Endpoints(url));

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData("UAT", "http://uat.rasid.example.com")]
    [InlineData("Production", "http://rasid.example.com")]
    [InlineData("Production", "http://localhost:8080")]
    public void Rejects_plain_http(string environment, string url)
    {
        var result = Validate(environment, Endpoints(url));

        Assert.True(result.Failed);
        Assert.Equal(3, result.Failures!.Count());
        Assert.All(result.Failures!, failure => Assert.Contains("must use HTTPS", failure, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("UAT")]
    public void X_api_always_requires_https(string environment)
    {
        var options = Endpoints("http://localhost:8080");
        options.XApi.BaseUrl = "http://localhost:9000/2/";

        var result = Validate(environment, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.StartsWith("Endpoints:XApi", StringComparison.Ordinal));
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(string environment, EndpointsOptions options) =>
        new EndpointsOptionsValidator(new TestEnvironment(environment)).Validate(null, options);

    private static EndpointsOptions Endpoints(string appUrl) => new()
    {
        XApi = new BaseUrlEndpoint { BaseUrl = "https://api.x.com/2/" },
        XWeb = new XWebEndpoint { PostUrlTemplate = "https://x.com/{username}/status/{postId}" },
        WebApp = new BaseUrlEndpoint { BaseUrl = appUrl },
        Api = new BaseUrlEndpoint { BaseUrl = $"{appUrl}/api" },
        Cors = new CorsEndpoint { AllowedOrigins = [appUrl] },
    };

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Rasid.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
