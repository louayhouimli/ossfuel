using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OSSFuel.Api.Database;

namespace OSSFuel.Api.Features.Discovery.Ingestion;

public class ProjectIndexingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectIndexingBackgroundService> _logger;
    private readonly TimeSpan _startupDelay;
    private readonly TimeSpan _pollInterval;

    public ProjectIndexingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectIndexingBackgroundService> logger,
        TimeSpan? startupDelay = null,
        TimeSpan? pollInterval = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _startupDelay = startupDelay ?? TimeSpan.FromSeconds(5);
        _pollInterval = pollInterval ?? TimeSpan.FromHours(24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_startupDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunBackfillAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during background project indexing backfill");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task<int> RunBackfillAsync(CancellationToken stoppingToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>();
        var ingestionService = scope.ServiceProvider.GetRequiredService<IProjectIngestionService>();

        var unindexedProjectIds = await db.Projects
            .Where(p => !db.ProjectChunks.Any(c => c.ProjectId == p.Id))
            .Select(p => p.Id)
            .ToListAsync(stoppingToken);

        if (unindexedProjectIds.Count == 0)
        {
            _logger.LogInformation("All registered projects are indexed for semantic discovery.");
            return 0;
        }

        _logger.LogInformation("Found {Count} unindexed projects. Starting background embedding backfill...", unindexedProjectIds.Count);

        var processed = 0;
        foreach (var projectId in unindexedProjectIds)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var result = await ingestionService.IngestProjectAsync(projectId, stoppingToken);
            if (result.Succeeded)
            {
                processed++;
                _logger.LogInformation("Auto-indexed project {FullName} ({Chunks} chunks)", result.ProjectFullName, result.ChunksCount);
            }
            else
            {
                _logger.LogWarning("Failed to auto-index project {ProjectId}: {Error}", projectId, result.Error);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Background embedding backfill completed. Processed {Count} projects.", processed);
        return processed;
    }
}
