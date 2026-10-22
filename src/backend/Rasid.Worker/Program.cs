using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Rasid.Application;
using Rasid.Application.Common.Abstractions;
using Rasid.Infrastructure;
using Rasid.Infrastructure.Configuration;
using Rasid.Infrastructure.Jobs;
using Rasid.Infrastructure.Services;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Configuration.AddEndpointsFile(builder.Environment);

builder.Services.AddSerilog((services, logger) =>
{
    logger.ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "rasid-worker");

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
    .AddInfrastructure(builder.Configuration);

// Background jobs act as the system identity (recorded in audit entries).
builder.Services.AddSingleton<ICurrentUser, SystemCurrentUser>();

builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"rasid-worker:{Environment.MachineName}";
    options.Queues = JobQueues.All;
    options.WorkerCount = builder.Configuration.GetValue("Worker:WorkerCount", 4);
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

// Operations dashboard: local requests only. The Worker port is never published on the public ingress.
app.UseHangfireDashboard("/jobs", new DashboardOptions
{
    Authorization = [new LocalRequestsOnlyAuthorizationFilter()],
    DashboardTitle = "Rasid — Background Jobs",
    DisplayStorageConnectionString = false,
    IsReadOnlyFunc = _ => !app.Environment.IsDevelopment(),
});

app.Services.GetRequiredService<IRecurringJobManager>().RegisterRasidJobs();

await app.RunAsync();
