namespace Rasid.Api.Endpoints;

/// <summary>A cohesive set of endpoints. Every implementation is discovered and mapped at start-up.</summary>
internal interface IEndpointGroup
{
    void Map(IEndpointRouteBuilder api);
}

internal static class EndpointGroupExtensions
{
    public const string ApiPrefix = "/api/v1";

    public static IEndpointRouteBuilder MapEndpointGroups(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup(ApiPrefix);

        var groups = typeof(IEndpointGroup).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpointGroup).IsAssignableFrom(t))
            .Select(t => (IEndpointGroup)Activator.CreateInstance(t)!);

        foreach (var group in groups)
        {
            group.Map(api);
        }

        return app;
    }
}
