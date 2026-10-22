using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Rasid.Api.Common;
using Rasid.Api.Endpoints;
using Rasid.Application;
using Rasid.Infrastructure;
using Rasid.Infrastructure.Configuration;
using Rasid.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Shared configuration files are copied next to the binaries (see the .csproj).
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Configuration.AddEndpointsFile(builder.Environment);

builder.Services.AddSerilog((services, logger) =>
{
    logger.ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "rasid-api");

    if (builder.Environment.IsDevelopment())
    {
        logger.WriteTo.Console();
    }
    else
    {
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddIdentityServices(builder.Configuration)
    .AddApiServices();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();

// Interactive API documentation (Swagger UI at /swagger, Scalar at /scalar): Development and UAT only.
// The UI pages are public; every API call made from them still needs a valid token.
var showApiDocs = !app.Environment.IsProduction();
if (showApiDocs)
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Rasid API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Rasid API — Swagger";
    });
}

app.UseCors(ApiServiceCollectionExtensions.CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (showApiDocs)
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

app.MapEndpointGroups();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
}

await app.RunAsync();
