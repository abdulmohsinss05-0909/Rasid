using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Dashboard;
using Rasid.Application.Monitoring;

namespace Rasid.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<ISender, Sender>();
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);

        // Error keys match the JSON contract (camelCase), e.g. "rules[0].value", so clients can bind them to fields.
        ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
            member is null ? null : System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(member.Name);
        RegisterRequestHandlers(services);

        services.AddScoped<IQuotaManager, QuotaManager>();
        services.AddScoped<PostWriter>();
        services.AddScoped<TopicAggregationService>();
        services.AddScoped<TopicIngestionService>();
        services.AddScoped<MetricsRefreshService>();
        services.AddScoped<RetentionService>();
        services.AddScoped<DiscoveryPlanner>();
        services.AddScoped<SourceTransitionService>();
        services.AddScoped<DataFreshnessBuilder>();

        return services;
    }

    /// <summary>Registers every IRequestHandler implementation in this assembly.</summary>
    private static void RegisterRequestHandlers(IServiceCollection services)
    {
        var handlerInterface = typeof(IRequestHandler<,>);

        var registrations = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .Select(i => (Service: i, Implementation: t)));

        foreach (var (service, implementation) in registrations)
        {
            services.AddScoped(service, implementation);
        }
    }
}
