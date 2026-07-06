using PosterMint.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PosterMint.Worker.Services;

public sealed class DatabaseHeartbeatWorker(
    IServiceProvider serviceProvider,
    ILogger<DatabaseHeartbeatWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        logger.LogInformation("Database heartbeat worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckDatabaseAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PosterMintDbContext>();
            var templateCount = await dbContext.Templates.CountAsync(cancellationToken);
            var shopCount = await dbContext.Shops.CountAsync(cancellationToken);

            logger.LogInformation(
                "Heartbeat OK. Templates: {TemplateCount}, Shops: {ShopCount}",
                templateCount,
                shopCount);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Heartbeat failed while checking database state.");
        }
    }
}
