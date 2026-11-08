using Microsoft.EntityFrameworkCore;

namespace Horica.ReviewService.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddReviewPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ReviewDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ReviewDb' is not configured. Set ConnectionStrings:ReviewDb (env var ConnectionStrings__ReviewDb).");
        }

        services.AddDbContext<ReviewDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

        return services;
    }
}
