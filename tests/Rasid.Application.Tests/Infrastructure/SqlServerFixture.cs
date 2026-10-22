using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Rasid.Application;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Connectors;
using Rasid.Infrastructure;
using Rasid.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Rasid.Application.Tests.Infrastructure;

/// <summary>
/// One SQL Server container per test run, with the real Application and Infrastructure wiring.
/// Only the outward-facing edges are replaced: the data source (controllable fake), the job queue and the caller identity.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public FakePostSource Source { get; } = new();

    public RecordingJobScheduler Jobs { get; } = new();

    /// <summary>Controllable clock shared by all services, so tests can move time forward.</summary>
    public Microsoft.Extensions.Time.Testing.FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Rasid"] = _container.GetConnectionString(),
                ["Endpoints:XApi:BaseUrl"] = "https://api.x.com/2/",
                ["Endpoints:XWeb:PostUrlTemplate"] = "https://x.com/{username}/status/{postId}",
                ["Endpoints:WebApp:BaseUrl"] = "http://localhost:4200",
                ["Endpoints:Api:BaseUrl"] = "http://localhost:5080",
                ["Monitoring:AllowSampleData"] = "true",
                ["Monitoring:DashboardCacheSeconds"] = "1",
                ["Monitoring:ManualRefreshCooldownSeconds"] = "0",
                ["Monitoring:Quota:MonthlyPostReadBudget"] = "1000",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddApplication().AddInfrastructure(configuration);
        services.AddSingleton<ICurrentUser, TestCurrentUser>();
        services.AddSingleton<IJobScheduler>(Jobs);
        services.AddSingleton<IPostSourceResolver>(new FixedResolver(Source));
        services.AddSingleton<TimeProvider>(Clock);
        Services = services.BuildServiceProvider();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RasidDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Removes all application data between tests (schema stays).</summary>
    public async Task ResetAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RasidDbContext>();
        await db.Topics.ExecuteDeleteAsync();
        await db.Posts.ExecuteDeleteAsync();
        await db.Authors.ExecuteDeleteAsync();
        await db.RefreshLogs.ExecuteDeleteAsync();
        await db.ApiUsagePeriods.ExecuteDeleteAsync();
        await db.AuditLogs.ExecuteDeleteAsync();
        Source.Reset();
        Jobs.Enqueued.Clear();
    }

    private sealed class FixedResolver(IPostSource source) : IPostSourceResolver
    {
        public IPostSource? Resolve() => source;
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public string Id => "test-user";

        public string Name => "Test User";

        public bool IsAuthenticated => true;

        public string? CorrelationId => "test";
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Rasid.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sql-server";
}

public sealed class RecordingJobScheduler : IJobScheduler
{
    public List<Guid> Enqueued { get; } = [];

    public string EnqueueTopicDiscovery(Guid topicId)
    {
        Enqueued.Add(topicId);
        return Enqueued.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
