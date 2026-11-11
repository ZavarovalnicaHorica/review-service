using Microsoft.OpenApi;

namespace Horica.ReviewService.Api;

public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddReviewSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Horica ReviewService",
                Version = "v1",
                Description = "Human-in-the-loop review of risky insurance claims."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT access token from auth-service or Keycloak (not needed when Auth:Mode=Disabled)."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
            });
        });

        return services;
    }
}
