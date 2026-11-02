using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StudentClubManagement.Web.Data;

namespace StudentClubManagement.Web.Health;

public class DatabaseReadinessCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Không kết nối được cơ sở dữ liệu.");
            }

            var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
            return pending.Any()
                ? HealthCheckResult.Unhealthy("Cơ sở dữ liệu còn migration chưa áp dụng.")
                : HealthCheckResult.Healthy();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Endpoint chỉ trả trạng thái; không công bố chi tiết cấu hình kết nối.
            return HealthCheckResult.Unhealthy("Cơ sở dữ liệu chưa sẵn sàng.");
        }
    }
}
