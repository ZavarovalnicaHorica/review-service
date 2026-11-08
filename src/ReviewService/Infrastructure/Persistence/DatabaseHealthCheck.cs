using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Horica.ReviewService.Infrastructure.Persistence;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly ReviewDbContext _db;

    public DatabaseHealthCheck(ReviewDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Cannot connect to the database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
