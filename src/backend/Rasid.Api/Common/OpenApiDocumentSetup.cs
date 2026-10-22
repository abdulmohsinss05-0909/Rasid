using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Rasid.Api.Common;

/// <summary>
/// Describes the API and its JWT bearer security in the generated OpenAPI document, so Swagger UI and
/// Scalar show an "Authorize" option. The document itself is produced by ASP.NET Core (/openapi/v1.json).
/// </summary>
internal sealed class OpenApiDocumentSetup : IOpenApiDocumentTransformer
{
    private const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info = new OpenApiInfo
        {
            Title = "Rasid API",
            Version = "v1",
            Description = "Monitoring of public X posts by topic. Sign in via POST /api/v1/auth/login, then use the returned access token with Authorize.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token from POST /api/v1/auth/login.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
        });

        return Task.CompletedTask;
    }
}
