using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Services;

public class DocumentProcessingBackgroundService : BackgroundService
{
    private readonly DocumentProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentProcessingBackgroundService> _logger;

    public DocumentProcessingBackgroundService(
        DocumentProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentProcessingBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document processing background service started");

        // Recover stuck jobs (status == Processing) on startup
        await RecoverStuckJobsAsync();

        // Periodic recovery: re-enqueue stuck jobs every 5 minutes
        _ = RunPeriodicRecoveryAsync(stoppingToken);

        await foreach (var jobId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing job {JobId}", jobId);
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IDocumentProcessor>();
                await processor.ProcessAsync(jobId);
                _logger.LogInformation("Completed processing job {JobId}", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing job {JobId}", jobId);
            }
        }
    }

    private async Task RunPeriodicRecoveryAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RecoverStuckJobsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Periodic stuck-job recovery failed");
            }
        }
    }

    private async Task RecoverStuckJobsAsync()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.PiiGatewayDbContext>();
            var stuckJobs = dbContext.Jobs
                .Where(j => j.Status == JobStatus.Processing
                         || (j.Status == JobStatus.Created && j.ErrorMessage == null))
                .Select(j => j.Id)
                .ToList();

            foreach (var jobId in stuckJobs)
            {
                _logger.LogWarning("Recovering stuck job {JobId}", jobId);
                await _queue.EnqueueAsync(jobId);
            }

            if (stuckJobs.Count > 0)
                _logger.LogInformation("Recovered {Count} stuck jobs", stuckJobs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover stuck jobs on startup");
        }
    }
}
