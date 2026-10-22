using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Rasid.Application.Auth;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Options;
using Rasid.Application.Connectors;
using Rasid.Infrastructure.Configuration;
using Rasid.Infrastructure.Connectors;
using Rasid.Infrastructure.Connectors.Sample;
using Rasid.Infrastructure.Connectors.X;
using Rasid.Infrastructure.Identity;
using Rasid.Infrastructure.Jobs;
using Rasid.Infrastructure.Persistence;
using Rasid.Infrastructure.Services;

namespace Rasid.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Rasid";
    public const string HangfireSchema = "hangfire";

    /// <summary>Services shared by the API and the Worker.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddSingleton(TimeProvider.System);
        services.AddOptionsWithValidation(configuration);

        services.AddDbContext<RasidDbContext>(options => options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 5);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", RasidDbContext.AppSchema);
        }));
        services.AddScoped<IRasidDbContext>(sp => sp.GetRequiredService<RasidDbContext>());

        services.AddHybridCache();
        services.AddConnectors();

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                () => new SqlConnection(connectionString),
                new SqlServerStorageOptions
                {
                    SchemaName = HangfireSchema,
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = TimeSpan.FromSeconds(5),
                }));

        services.AddScoped<IJobScheduler, HangfireJobScheduler>();
        services.AddSingleton<IDistributedLock, HangfireDistributedLock>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<DiscoveryJob>();
        services.AddScoped<DiscoverySchedulerJob>();
        services.AddScoped<MetricsRefreshJob>();
        services.AddScoped<RetentionJob>();

        services.AddHealthChecks()
            .AddDbContextCheck<RasidDbContext>("database", tags: ["ready"])
            .AddCheck<DataSourceHealthCheck>("data-source", tags: ["ready"]);

        return services;
    }

    /// <summary>Identity, JWT issuing and database seeding — used by the API host only.</summary>
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName));

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<RasidDbContext>();

        services.AddScoped<TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }

    private static void AddOptionsWithValidation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EndpointsOptions>().Bind(configuration.GetSection(EndpointsOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<EndpointsOptions>, EndpointsOptionsValidator>();

        services.AddOptions<MonitoringOptions>()
            .Bind(configuration.GetSection(MonitoringOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.Engagement.HighAbovePercent > o.Engagement.MediumFromPercent, "Engagement: HighAbovePercent must exceed MediumFromPercent.")
            .Validate(o => o.Quota.CriticalAtPercent > o.Quota.ThrottleAtPercent, "Quota: CriticalAtPercent must exceed ThrottleAtPercent.")
            .ValidateOnStart();

        services.AddOptions<XApiOptions>()
            .Bind(configuration.GetSection(XApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void AddConnectors(this IServiceCollection services)
    {
        services.AddTransient<XAuthHandler>();
        services.AddTransient<XRateLimitHandler>();

        services.AddHttpClient<XApiClient>((sp, client) =>
            {
                client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<EndpointsOptions>>().Value.XApi.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Rasid/1.0");
                client.Timeout = Timeout.InfiniteTimeSpan; // Timeouts are owned by the resilience pipeline.
            })
            .AddHttpMessageHandler<XRateLimitHandler>()
            .AddHttpMessageHandler<XAuthHandler>()
            .AddResilienceHandler("x-api", (pipeline, context) =>
            {
                var x = context.ServiceProvider.GetRequiredService<IOptions<XApiOptions>>().Value;

                // 429 is deliberately NOT retried: X tells us when the window resets and we honour it.
                static ValueTask<bool> IsTransient(Outcome<HttpResponseMessage> outcome) => ValueTask.FromResult(
                    HttpClientResiliencePredicates.IsTransient(outcome)
                    && outcome.Result?.StatusCode != System.Net.HttpStatusCode.TooManyRequests);

                pipeline
                    .AddTimeout(TimeSpan.FromSeconds(x.TotalTimeoutSeconds))
                    .AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = Math.Max(1, x.MaxRetries),
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromSeconds(1),
                        ShouldHandle = args => IsTransient(args.Outcome),
                    })
                    .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                    {
                        FailureRatio = 0.5,
                        MinimumThroughput = 5,
                        SamplingDuration = TimeSpan.FromMinutes(1),
                        BreakDuration = TimeSpan.FromMinutes(1),
                        ShouldHandle = args => IsTransient(args.Outcome),
                    })
                    .AddTimeout(TimeSpan.FromSeconds(x.AttemptTimeoutSeconds));
            });

        services.AddScoped<XPostSource>();
        services.AddSingleton<SamplePostSource>();
        services.AddScoped<IPostSourceResolver, PostSourceResolver>();
    }
}
