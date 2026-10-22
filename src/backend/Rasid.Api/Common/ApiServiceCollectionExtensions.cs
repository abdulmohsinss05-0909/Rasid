using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Rasid.Application.Auth;
using Rasid.Application.Common.Abstractions;
using Rasid.Infrastructure.Configuration;
using Rasid.Infrastructure.Identity;

namespace Rasid.Api.Common;

internal static class ApiServiceCollectionExtensions
{
    public const string CorsPolicy = "rasid-web";

    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] =
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        });

        services.AddOpenApi(options => options.AddDocumentTransformer<OpenApiDocumentSetup>());
        services.AddJwtAuthentication();
        services.AddAuthorizationPolicies();
        services.AddRateLimiting();
        services.AddCorsFromEndpoints();

        return services;
    }

    private static void AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from validated JwtOptions (the signing key comes from the secret store).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TokenService.SigningKey(jwt.Value),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = TokenService.NameClaim,
                    RoleClaimType = TokenService.RoleClaim,
                };
            });
    }

    private static void AddAuthorizationPolicies(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.CanView, p => p.RequireRole(Roles.Viewer, Roles.Analyst, Roles.Admin))
            .AddPolicy(Policies.CanManageTopics, p => p.RequireRole(Roles.Analyst, Roles.Admin))
            .AddPolicy(Policies.CanAdminister, p => p.RequireRole(Roles.Admin))
            // Deny by default: every endpoint requires an authenticated user unless explicitly anonymous.
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

    private static void AddRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // General API limit, per user (or per IP when anonymous).
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    context.User.Identity?.IsAuthenticated == true
                        ? $"user:{context.User.FindFirst(TokenService.SubjectClaim)?.Value}"
                        : $"ip:{context.Connection.RemoteIpAddress}",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 120,
                        TokensPerPeriod = 120,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // Sign-in and token endpoints: strict, per IP.
            options.AddPolicy(RateLimitPolicies.Auth, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"auth:{context.Connection.RemoteIpAddress}",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

    private static void AddCorsFromEndpoints(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IOptions<EndpointsOptions>>((cors, endpoints) =>
                cors.AddPolicy(CorsPolicy, policy => policy
                    .WithOrigins([.. endpoints.Value.Cors.AllowedOrigins])
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .WithHeaders("Authorization", "Content-Type")
                    .AllowCredentials()));
    }
}
