using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rasid.Application.Auth;
using Rasid.Domain.Topics;
using Rasid.Infrastructure.Identity;

namespace Rasid.Infrastructure.Persistence;

/// <summary>Seeding settings. Passwords come from the secret store only.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Apply EF migrations at API start-up (Development/UAT). Production uses the pipeline's migrator.</summary>
    public bool ApplyMigrations { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    /// <summary>Creates the demonstration topic when no topic exists yet.</summary>
    public bool CreateSampleTopic { get; set; }
}

public sealed partial class DatabaseInitializer(
    RasidDbContext db,
    RoleManager<IdentityRole> roleManager,
    UserManager<AppUser> userManager,
    IOptions<SeedOptions> options,
    TimeProvider clock,
    ILogger<DatabaseInitializer> logger)
{
    public const string SampleTopicName = "UAE Humanitarian Aid";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (settings.ApplyMigrations)
        {
            await db.Database.MigrateAsync(cancellationToken);
            LogMigrated();
        }

        await EnsureRolesAsync();
        await EnsureAdminAsync(settings);

        if (settings.CreateSampleTopic)
        {
            await EnsureSampleTopicAsync(cancellationToken);
        }
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole(role)), $"create role {role}");
            }
        }
    }

    private async Task EnsureAdminAsync(SeedOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AdminEmail) || string.IsNullOrWhiteSpace(settings.AdminPassword))
        {
            LogNoAdminSeed();
            return;
        }

        if (await userManager.FindByEmailAsync(settings.AdminEmail) is not null)
        {
            return;
        }

        var admin = new AppUser
        {
            UserName = settings.AdminEmail,
            Email = settings.AdminEmail,
            EmailConfirmed = true,
            DisplayName = "Administrator",
            CreatedUtc = clock.GetUtcNow(),
        };

        ThrowIfFailed(await userManager.CreateAsync(admin, settings.AdminPassword), "create admin user");
        ThrowIfFailed(await userManager.AddToRoleAsync(admin, Roles.Admin), "assign admin role");
        LogAdminCreated(settings.AdminEmail);
    }

    private async Task EnsureSampleTopicAsync(CancellationToken cancellationToken)
    {
        if (await db.Topics.AnyAsync(cancellationToken))
        {
            return;
        }

        var topic = Topic.Create(
            SampleTopicName,
            "Demonstration topic: public conversation about UAE humanitarian and development aid.",
            TopicPriority.High,
            [
                TopicRule.Create(RuleType.Keyword, "UAE aid"),
                TopicRule.Create(RuleType.Keyword, "humanitarian aid"),
                TopicRule.Create(RuleType.Hashtag, "UAEAid"),
                TopicRule.Create(RuleType.Hashtag, "HumanitarianAid"),
                TopicRule.Create(RuleType.Keyword, "giveaway", isExclusion: true),
            ],
            createdBy: "system",
            clock.GetUtcNow());

        db.Topics.Add(topic);
        await db.SaveChangesAsync(cancellationToken);
        LogSampleTopicCreated(topic.Name);
    }

    private static void ThrowIfFailed(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {operation}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied")]
    private partial void LogMigrated();

    [LoggerMessage(Level = LogLevel.Information, Message = "No admin seed credentials configured; skipping admin creation")]
    private partial void LogNoAdminSeed();

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin user {Email} created")]
    private partial void LogAdminCreated(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample topic '{Name}' created")]
    private partial void LogSampleTopicCreated(string name);
}
