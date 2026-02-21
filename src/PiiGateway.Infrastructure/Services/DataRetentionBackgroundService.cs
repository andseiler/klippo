using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Options;

namespace PiiGateway.Infrastructure.Services;

public class DataRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRetentionBackgroundService> _logger;
    private readonly DataRetentionOptions _options;
    private readonly GuestDemoOptions _guestOptions;

    public DataRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<DataRetentionOptions> options,
        IOptions<GuestDemoOptions> guestOptions,
        ILogger<DataRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _guestOptions = guestOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data retention service started (retention: {Days} days for jobs, {AuditDays} days for audit logs)",
            _options.CompletedJobRetentionDays, _options.AuditLogRetentionDays);

        // Guest cleanup runs on its own timer
        _ = RunGuestCleanupLoopAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        // Run once on startup (after a short delay)
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        await RunRetentionAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunRetentionAsync(stoppingToken);
        }
    }

    private async Task RunGuestCleanupLoopAsync(CancellationToken stoppingToken)
    {
        if (!_guestOptions.Enabled) return;

        var interval = TimeSpan.FromMinutes(_guestOptions.CleanupIntervalMinutes);
        using var guestTimer = new PeriodicTimer(interval);

        // Initial delay
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        await RunGuestCleanupAsync(stoppingToken);

        while (await guestTimer.WaitForNextTickAsync(stoppingToken))
        {
            await RunGuestCleanupAsync(stoppingToken);
        }
    }

    private async Task RunGuestCleanupAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
            var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            var cutoff = DateTime.UtcNow.AddMinutes(-_guestOptions.TokenExpiryMinutes);
            var guestJobs = await jobRepository.GetExpiredGuestJobsAsync(cutoff);

            var deletedCount = 0;
            foreach (var job in guestJobs)
            {
                if (stoppingToken.IsCancellationRequested) break;
                try
                {
                    try { await fileStorageService.DeleteAsync(job.Id, job.FileType); } catch { }
                    await auditLogRepository.DeleteByJobIdAsync(job.Id);
                    await jobRepository.DeleteAsync(job);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete guest job {JobId}", job.Id);
                }
            }

            if (deletedCount > 0)
                _logger.LogInformation("Guest cleanup: deleted {Count} expired guest jobs", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Guest cleanup failed");
        }
    }

    private async Task RunRetentionAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Running data retention cleanup");

            await using var scope = _scopeFactory.CreateAsyncScope();
            var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
            var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            var jobCutoff = DateTime.UtcNow.AddDays(-_options.CompletedJobRetentionDays);
            var terminalJobs = await jobRepository.GetTerminalJobsOlderThanAsync(jobCutoff);

            var deletedCount = 0;
            foreach (var job in terminalJobs)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    // Delete file from storage
                    try { await fileStorageService.DeleteAsync(job.Id, job.FileType); } catch { }

                    // Delete audit logs first (FK constraint)
                    await auditLogRepository.DeleteByJobIdAsync(job.Id);

                    // Delete job (cascades to segments + entities)
                    await jobRepository.DeleteAsync(job);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete job {JobId} during retention cleanup", job.Id);
                }
            }

            if (deletedCount > 0)
                _logger.LogInformation("Data retention: deleted {Count} expired jobs", deletedCount);
            else
                _logger.LogDebug("Data retention: no expired jobs found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data retention cleanup failed");
        }
    }
}
