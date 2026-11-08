using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Horica.ReviewService.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddReviewAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

        if (auth.IsDisabled)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Auth:Mode=Disabled is only allowed when ASPNETCORE_ENVIRONMENT=Development. " +
                    "Configure Auth:Authority (Keycloak realm URL) for any other environment.");
            }

            services
                .AddAuthentication(DevAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(auth.Authority))
            {
                throw new InvalidOperationException(
                    "Auth:Authority is not configured. Set it to your Keycloak realm URL " +
                    "(env var Auth__Authority), or use Auth__Mode=Disabled in Development.");
            }

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = auth.Authority;
                    if (!string.IsNullOrWhiteSpace(auth.MetadataAddress))
                    {
                        options.MetadataAddress = auth.MetadataAddress;
                    }

                    options.RequireHttpsMetadata = auth.RequireHttpsMetadata;
                    options.MapInboundClaims = false;

                    var parameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = !string.IsNullOrWhiteSpace(auth.Audience),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = "preferred_username",
                        RoleClaimType = ClaimTypes.Role
                    };

                    if (!string.IsNullOrWhiteSpace(auth.Audience))
                    {
                        parameters.ValidAudience = auth.Audience;
                    }

                    if (!string.IsNullOrWhiteSpace(auth.ValidIssuer))
                    {
                        parameters.ValidIssuer = auth.ValidIssuer;
                    }

                    options.TokenValidationParameters = parameters;

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            if (context.Principal is not null)
                            {
                                KeycloakRoleMapper.AddRoleClaims(context.Principal, auth.ClientId);
                            }

                            return Task.CompletedTask;
                        }
                    };
                });
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.Reviewer, policy =>
                policy.RequireAuthenticatedUser().RequireRole(auth.ReviewerRole, auth.AdminRole));
        });

        return services;
    }
}
